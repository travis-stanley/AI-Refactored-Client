#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Optimization;
using AIRefactored.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    public class BotPanicHandler : MonoBehaviour
    {
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

        /// <summary>
        /// True if the bot is currently panicking.
        /// </summary>
        public bool IsPanicking => _isPanicking;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();

            var health = _bot?.GetPlayer?.HealthController as HealthControllerClass;
            if (health != null)
                health.ApplyDamageEvent += OnDamaged;
        }

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
                StartPanic(time, _bot!.LookDirection);
            }
            else if (CheckNearbySquadDanger(out Vector3 retreatDir))
            {
                StartPanic(time, retreatDir);
            }
        }

        private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
        {
            if (_isPanicking || !IsValid())
                return;

            var profile = BotRegistry.Get(_bot!.ProfileId);
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
            if (!IsValid())
                return false;

            var profile = BotRegistry.Get(_bot!.ProfileId);
            if (profile == null || profile.IsFrenzied || profile.IsStubborn)
                return false;

            if (_cache!.FlashGrenade != null && _cache.FlashGrenade.IsFlashed())
                return true;

            var hp = _bot.HealthController.GetBodyPartHealth(EBodyPart.Common);
            return hp.Current < 25f;
        }

        private bool CheckNearbySquadDanger(out Vector3 retreatDir)
        {
            retreatDir = Vector3.zero;

            string? groupId = _bot?.Profile?.Info?.GroupId;
            if (string.IsNullOrEmpty(groupId))
                return false;

            List<BotMemoryStore.DangerZone> zones = BotMemoryStore.GetZonesForMap(GameWorldHandler.GetCurrentMapName());
            Vector3 pos = _bot!.Position;

            for (int i = 0; i < zones.Count; i++)
            {
                float distSqr = (zones[i].Position - pos).sqrMagnitude;
                if (distSqr <= SquadPanicRadiusSqr)
                {
                    retreatDir = (pos - zones[i].Position).normalized;
                    return true;
                }
            }

            return false;
        }

        private void StartPanic(float now, Vector3 retreatDir)
        {
            if (!IsValid())
                return;

            _isPanicking = true;
            _panicStartTime = now;
            _composureLevel = 0f;

            BotPersonalityProfile? profile = BotRegistry.Get(_bot!.ProfileId);
            float cohesion = profile != null ? profile.Cohesion : 1f;

            List<Vector3> path = (_cache!.PathCache != null)
                ? BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, retreatDir, _cache.PathCache)
                : new List<Vector3> { _bot.Position + retreatDir * 8f };

            if (path.Count >= 2)
                BotMovementHelper.SmoothMoveTo(_bot, path[1], false, cohesion);

            BotMemoryStore.AddDangerZone(GameWorldHandler.GetCurrentMapName(), _bot.Position, DangerTriggerType.Panic, 0.6f);

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

        private void RecoverComposure(float deltaTime)
        {
            _composureLevel = Mathf.Clamp01(_composureLevel + deltaTime * RecoverySpeed);
        }

        public float GetComposureLevel()
        {
            return _composureLevel;
        }

        public void TriggerPanic()
        {
            if (_isPanicking || !IsValid())
                return;

            if (Time.time < _lastPanicExitTime + PanicCooldown)
                return;

            StartPanic(Time.time, -_bot!.LookDirection);
        }

        private bool IsValid()
        {
            return _bot != null &&
                   _cache != null &&
                   !_bot.IsDead &&
                   _bot.GetPlayer != null &&
                   _bot.GetPlayer.IsAI;
        }
    }
}
