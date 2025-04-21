#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Optimization;
using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Handles bot panic behavior: suppression, flashbangs, low-health, and squad-based panic chaining.
    /// </summary>
    public class BotPanicHandler
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _panicStartTime = -1f;
        private float _lastPanicExitTime = -99f;
        private float _composureLevel = 1f;
        private bool _isPanicking = false;

        private const float PanicDuration = 3.5f;
        private const float PanicCooldown = 5.0f;
        private const float RecoverySpeed = 0.2f;
        private const float SquadPanicRadiusSqr = 15f * 15f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Public Properties

        /// <summary>
        /// Whether the bot is currently panicking and unable to engage targets.
        /// </summary>
        public bool IsPanicking => _isPanicking;

        /// <summary>
        /// Last known profile ID of the player that hit the bot.
        /// </summary>
        public string? LastHitSource { get; private set; }

        /// <summary>
        /// Last known profile ID of the player that suppressed the bot.
        /// </summary>
        public string? LastSuppressionSource { get; private set; }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the panic handler with references and binds health damage triggers.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;

            if (_bot?.GetPlayer?.HealthController is HealthControllerClass health)
            {
                health.ApplyDamageEvent += OnDamaged;
            }
        }

        #endregion

        #region Tick Logic

        /// <summary>
        /// Called every tick to update panic state and recovery.
        /// </summary>
        public void Tick(float time)
        {
            if (!IsValid())
                return;

            if (_isPanicking)
            {
                if (time - _panicStartTime > PanicDuration)
                    EndPanic(time);
                return;
            }

            RecoverComposure(Time.deltaTime);

            if (time <= _lastPanicExitTime + PanicCooldown)
                return;

            if (ShouldTriggerPanic())
            {
                StartPanic(time, -_bot!.LookDirection);
            }
            else if (CheckNearbySquadDanger(out Vector3 retreatDir))
            {
                StartPanic(time, retreatDir);
            }
        }

        #endregion

        #region Panic Triggers

        private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
        {
            if (!IsValid() || _isPanicking || Time.time < _lastPanicExitTime + PanicCooldown)
                return;

            var profile = BotRegistry.Get(_bot!.ProfileId);
            if (profile == null || profile.IsFrenzied || profile.IsStubborn || profile.AggressionLevel > 0.8f)
                return;

            Vector3 threatDir = (_bot.Position - info.HitPoint).normalized;
            StartPanic(Time.time, threatDir);

            var source = _bot.Memory?.GoalEnemy?.Person;
            LastHitSource = source?.ProfileId ?? "unknown";

            _cache?.LastShotTracker?.RegisterHitBy(source);
            _cache?.InjurySystem?.OnHit(part, damage);
            _cache?.GroupComms?.SayHit();
        }

        private bool ShouldTriggerPanic()
        {
            if (!IsValid())
                return false;

            var profile = BotRegistry.Get(_bot!.ProfileId);
            if (profile == null || profile.IsFrenzied || profile.IsStubborn)
                return false;

            if (_cache?.FlashGrenade?.IsFlashed() == true)
                return true;

            var hp = _bot.HealthController.GetBodyPartHealth(EBodyPart.Common);
            return hp.Current < 25f;
        }

        private bool CheckNearbySquadDanger(out Vector3 retreatDir)
        {
            retreatDir = Vector3.zero;

            if (string.IsNullOrEmpty(_bot?.Profile?.Info?.GroupId))
                return false;

            List<BotMemoryStore.DangerZone> zones = BotMemoryStore.GetZonesForMap(GameWorldHandler.GetCurrentMapName());
            Vector3 pos = _bot!.Position;

            foreach (var zone in zones)
            {
                if ((zone.Position - pos).sqrMagnitude <= SquadPanicRadiusSqr)
                {
                    retreatDir = (pos - zone.Position).normalized;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// External trigger to manually induce panic behavior.
        /// </summary>
        public void TriggerPanic()
        {
            if (!IsValid() || _isPanicking || Time.time < _lastPanicExitTime + PanicCooldown)
                return;

            var profile = BotRegistry.Get(_bot!.ProfileId);
            if (profile == null || profile.IsFrenzied || profile.IsStubborn)
                return;

            StartPanic(Time.time, -_bot!.LookDirection);
        }

        #endregion

        #region Panic Lifecycle

        private void StartPanic(float now, Vector3 retreatDir)
        {
            if (!IsValid())
                return;

            _isPanicking = true;
            _panicStartTime = now;
            _composureLevel = 0f;

            var profile = BotRegistry.Get(_bot!.ProfileId);
            float cohesion = profile?.Cohesion ?? 1f;

            Vector3 fallback = _bot.Position + retreatDir * 8f;
            List<Vector3> path = (_cache?.PathCache != null)
                ? BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, retreatDir, _cache.PathCache)
                : new List<Vector3> { fallback };

            if (path.Count >= 2)
                BotMovementHelper.SmoothMoveTo(_bot, path[1], false, cohesion);
            else
                BotMovementHelper.SmoothMoveTo(_bot, fallback, false, cohesion);

            BotMemoryStore.AddDangerZone(GameWorldHandler.GetCurrentMapName(), _bot.Position, DangerTriggerType.Panic, 0.6f);

            var source = _bot.Memory?.GoalEnemy?.Person;
            LastSuppressionSource = source?.ProfileId ?? "unknown";

            _bot.Sprint(true);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
        }

        private void EndPanic(float now)
        {
            _isPanicking = false;
            _lastPanicExitTime = now;

            if (_bot?.Memory != null)
            {
                _bot.Memory.SetLastTimeSeeEnemy();
                _bot.Memory.CheckIsPeace();
            }
        }

        #endregion

        #region Composure

        private void RecoverComposure(float deltaTime)
        {
            _composureLevel = Mathf.Clamp01(_composureLevel + deltaTime * RecoverySpeed);
        }

        public float GetComposureLevel()
        {
            return _composureLevel;
        }

        #endregion

        #region Validation

        private bool IsValid()
        {
            return _bot != null &&
                   _cache != null &&
                   !_bot.IsDead &&
                   _bot.GetPlayer != null &&
                   _bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
