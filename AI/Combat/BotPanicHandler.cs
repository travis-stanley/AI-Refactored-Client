#nullable enable

using System.Collections.Generic;
using UnityEngine;
using EFT;
using EFT.HealthSystem;
using Comfort.Common;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Helpers;
using AIRefactored.Core;
using UnityEngine.Profiling;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Handles panic behavior for bots, including squad-aware fallback, flash response, and damage triggers.
    /// Supports composure-based recovery and personality-specific logic.
    /// </summary>
    public class BotPanicHandler : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _panicStartTime = -1f;
        private float _lastPanicExitTime = -99f;
        private bool _isPanicking = false;

        private float _composureLevel = 1f;

        private const float PanicDuration = 3.5f;
        private const float PanicCooldown = 5.0f;
        private const float RecoverySpeed = 0.2f;
        private const float SquadPanicRadiusSqr = 15f * 15f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();

            if (_bot?.GetPlayer?.HealthController is HealthControllerClass health)
            {
                health.ApplyDamageEvent += OnDamaged;
            }
        }

        private void Update()
        {
            if (_bot == null || _cache == null || IsHumanPlayer() || _bot.IsDead)
                return;

            float now = Time.time;

            if (_isPanicking)
            {
                if (now - _panicStartTime > PanicDuration)
                {
                    EndPanic(now);
                }
                return;
            }

            RecoverComposure(Time.deltaTime);

            if (now <= _lastPanicExitTime + PanicCooldown)
                return;

            if (ShouldTriggerPanic())
            {
                StartPanic(now, _bot.LookDirection);
                return;
            }

            if (CheckNearbySquadDanger(out Vector3 retreatDir))
            {
                StartPanic(now, retreatDir);
            }
        }

        #endregion

        #region Panic Triggers

        private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
        {
            if (_isPanicking || _bot == null || _cache == null || _bot.IsDead)
                return;

            var profile = BotRegistry.Get(_bot.ProfileId);
            if (profile == null || profile.IsFrenzied || profile.IsStubborn || profile.AggressionLevel > 0.8f)
                return;

            float now = Time.time;
            if (now < _lastPanicExitTime + PanicCooldown)
                return;

            Vector3 threatDir = (_bot.Position - info.HitPoint).normalized;
            StartPanic(now, threatDir);
        }

        private bool ShouldTriggerPanic()
        {
            if (_bot == null || _cache == null)
                return false;

            var profile = BotRegistry.Get(_bot.ProfileId);
            if (profile == null || profile.IsFrenzied || profile.IsStubborn)
                return false;

            if (_cache.FlashGrenade?.IsFlashed() == true)
                return true;

            var hp = _bot.HealthController.GetBodyPartHealth(EBodyPart.Common);
            return hp.Current < 25f;
        }

        private bool CheckNearbySquadDanger(out Vector3 retreatDir)
        {
            retreatDir = Vector3.zero;

            if (_bot?.Profile?.Info?.GroupId is not { } groupId || string.IsNullOrEmpty(groupId))
                return false;

            string mapId = GameWorldHandler.GetCurrentMapName();
            List<BotMemoryStore.DangerZone> zones = BotMemoryStore.GetZonesForMap(mapId);
            Vector3 botPos = _bot.Position;

            foreach (var zone in zones)
            {
                float sqrDist = (zone.Position - botPos).sqrMagnitude;
                if (sqrDist <= SquadPanicRadiusSqr)
                {
                    retreatDir = (botPos - zone.Position).normalized;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Panic Execution

        private void StartPanic(float now, Vector3 retreatDir)
        {
            if (_bot == null || _cache == null)
                return;

            _isPanicking = true;
            _panicStartTime = now;
            _composureLevel = 0f;

            var profile = BotRegistry.Get(_bot.ProfileId);
            float cohesion = profile?.Cohesion ?? 1.0f;

            List<Vector3> path = _cache.PathCache != null
                ? BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, retreatDir, _cache.PathCache)
                : new List<Vector3> { _bot.Position + retreatDir * 8f };

            if (path.Count >= 2)
            {
                Vector3 target = path[1];
                BotMovementHelper.SmoothMoveTo(_bot, target, allowSlowEnd: false, cohesionScale: cohesion);
            }

            BotMemoryStore.AddDangerZone(
                GameWorldHandler.GetCurrentMapName(),
                _bot.Position,
                DangerTriggerType.Panic,
                0.6f
            );

            _bot.Sprint(true);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
        }



        private void EndPanic(float now)
        {
            if (_bot == null)
                return;

            _isPanicking = false;
            _lastPanicExitTime = now;

            _bot.Memory.SetLastTimeSeeEnemy();
            _bot.Memory.CheckIsPeace();
        }

        private void RecoverComposure(float deltaTime)
        {
            _composureLevel = Mathf.Clamp01(_composureLevel + deltaTime * RecoverySpeed);
        }

        #endregion

        #region Public API

        public float GetComposureLevel() => _composureLevel;

        public void TriggerPanic()
        {
            if (_isPanicking || _bot == null || _cache == null || IsHumanPlayer())
                return;

            float now = Time.time;
            if (now < _lastPanicExitTime + PanicCooldown)
                return;

            StartPanic(now, -_bot.LookDirection);
        }

        #endregion

        #region Helpers

        private bool IsHumanPlayer()
        {
            return _bot?.GetPlayer != null && !_bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
