#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Optimization;
using AIRefactored.Core;
using EFT;
using System;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Handles suppression, flash, injury, and squad danger panic behavior.
    /// Manages composure, retreat direction, danger zones, and voice triggers.
    /// </summary>
    public sealed class BotPanicHandler
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _panicStartTime = -1f;
        private float _lastPanicExitTime = -99f;
        private float _composureLevel = 1f;
        private bool _isPanicking;

        private string? _lastHitSource;
        private string? _lastSuppressionSource;

        private const float PanicDuration = 3.5f;
        private const float PanicCooldown = 5.0f;
        private const float RecoverySpeed = 0.2f;
        private const float SquadRadiusSqr = 15f * 15f;

        #endregion

        #region Properties

        public bool IsPanicking => _isPanicking;
        public bool IsUnderSuppression => _isPanicking;
        public float GetComposureLevel() => _composureLevel;
        public string? LastHitSource => _lastHitSource;
        public string? LastSuppressionSource => _lastSuppressionSource;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _bot = cache.Bot ?? throw new ArgumentNullException(nameof(cache.Bot));

            var health = _bot.GetPlayer?.HealthController;
            if (health != null)
                health.ApplyDamageEvent += OnDamaged;
        }

        #endregion

        #region Tick Logic

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

            if (ShouldPanicFromThreat())
            {
                StartPanic(time, -_bot!.LookDirection);
            }
            else if (CheckNearbySquadDanger(out Vector3 retreatDir))
            {
                StartPanic(time, retreatDir);
            }
        }

        #endregion

        #region Triggers

        public void TriggerPanic()
        {
            if (_bot == null || !IsValid() || _isPanicking || Time.time < _lastPanicExitTime + PanicCooldown)
                return;

            var profile = BotRegistry.Get(_bot.ProfileId);
            if (profile == null || profile.IsFrenzied || profile.IsStubborn)
                return;

            StartPanic(Time.time, -_bot.LookDirection);
        }

        private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
        {
            if (!IsValid() || _isPanicking || Time.time < _lastPanicExitTime + PanicCooldown)
                return;

            var profile = BotRegistry.Get(_bot!.ProfileId);
            if (profile == null || profile.IsFrenzied || profile.IsStubborn || profile.AggressionLevel > 0.8f)
                return;

            Vector3 retreatDir = (_bot.Position - info.HitPoint).normalized;
            StartPanic(Time.time, retreatDir);

            var source = _bot.Memory?.GoalEnemy?.Person;
            _lastHitSource = source?.ProfileId ?? "unknown";

            _cache?.LastShotTracker?.RegisterHitBy(source);
            _cache?.InjurySystem?.OnHit(part, damage);
            _cache?.GroupComms?.SayHit();
        }

        #endregion

        #region Evaluation

        private bool ShouldPanicFromThreat()
        {
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
            if (_bot?.Profile?.Info?.GroupId == null)
                return false;

            var zones = BotMemoryStore.GetZonesForMap(GameWorldHandler.GetCurrentMapName());
            Vector3 myPos = _bot.Position;

            foreach (var zone in zones)
            {
                if ((zone.Position - myPos).sqrMagnitude <= SquadRadiusSqr)
                {
                    retreatDir = (myPos - zone.Position).normalized;
                    return true;
                }
            }

            return false;
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

            float cohesion = BotRegistry.Get(_bot!.ProfileId)?.Cohesion ?? 1f;
            Vector3 fallback = _bot.Position + retreatDir.normalized * 8f;

            if (_cache?.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, retreatDir, _cache.PathCache);
                if (path.Count > 0)
                {
                    fallback = (Vector3.Distance(path[0], _bot.Position) < 1f && path.Count > 1)
                        ? path[1]
                        : path[0];
                }
            }

            BotMovementHelper.SmoothMoveTo(_bot, fallback, false, cohesion);
            BotCoverHelper.TrySetStanceFromNearbyCover(_cache!, fallback);

            BotMemoryStore.AddDangerZone(
                GameWorldHandler.GetCurrentMapName(),
                _bot.Position,
                DangerTriggerType.Panic,
                0.6f
            );

            _lastSuppressionSource = _bot.Memory?.GoalEnemy?.Person?.ProfileId ?? "unknown";

            _bot.Sprint(true);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
        }

        private void EndPanic(float now)
        {
            _isPanicking = false;
            _lastPanicExitTime = now;

            _bot?.Memory?.SetLastTimeSeeEnemy();
            _bot?.Memory?.CheckIsPeace();
        }

        #endregion

        #region Utility

        private void RecoverComposure(float deltaTime)
        {
            _composureLevel = Mathf.Clamp01(_composureLevel + deltaTime * RecoverySpeed);
        }

        private bool IsValid()
        {
            return _bot != null &&
                   _cache != null &&
                   !_bot.IsDead &&
                   _bot.GetPlayer?.IsAI == true;
        }

        #endregion
    }
}
