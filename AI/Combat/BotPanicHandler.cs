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

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Handles bot panic logic when damaged, blinded, or responding to squad-based danger zones.
    /// Composure recovers gradually. Personality traits influence resistance to panic.
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
        private const float SquadPanicRadius = 15f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();

            if (_bot?.GetPlayer?.HealthController is HealthControllerClass health)
                health.ApplyDamageEvent += OnDamaged;
        }

        private void Update()
        {
            if (_bot == null || _cache == null || IsHumanPlayer())
                return;

            float now = Time.time;

            if (!_isPanicking)
            {
                RecoverComposure(Time.deltaTime);

                if (now > _lastPanicExitTime + PanicCooldown)
                {
                    if (ShouldTriggerPanic())
                    {
                        StartPanic(now, _bot.LookDirection);
                        return;
                    }

                    if (CheckNearbySquadDanger(out Vector3 retreatDir))
                        StartPanic(now, retreatDir);
                }
            }
            else if (now - _panicStartTime > PanicDuration)
            {
                EndPanic(now);
            }
        }

        #endregion

        #region Damage Trigger

        private void OnDamaged(EBodyPart part, float damage, DamageInfoStruct info)
        {
            if (_isPanicking || _bot == null || _cache == null || _bot.IsDead)
                return;

            var profile = BotRegistry.Get(_bot.ProfileId);
            if (profile.IsFrenzied || profile.IsStubborn || profile.AggressionLevel > 0.8f)
                return;

            float now = Time.time;
            if (now < _lastPanicExitTime + PanicCooldown)
                return;

            Vector3 threatDir = (_bot.Position - info.HitPoint).normalized;
            StartPanic(now, threatDir);
        }

        #endregion

        #region Panic Triggers

        private bool ShouldTriggerPanic()
        {
            if (_bot == null || _cache == null) return false;

            var profile = BotRegistry.Get(_bot.ProfileId);
            if (profile.IsFrenzied || profile.IsStubborn)
                return false;

            if (_cache.FlashGrenade != null && _cache.FlashGrenade.IsFlashed())
                return true;

            var hp = _bot.HealthController.GetBodyPartHealth(EBodyPart.Common);
            return hp.Current < 25f;
        }

        private bool CheckNearbySquadDanger(out Vector3 retreatDir)
        {
            retreatDir = Vector3.zero;
            if (_bot == null || _bot.Profile?.Info?.GroupId == null)
                return false;

            string groupId = _bot.Profile.Info.GroupId;
            if (string.IsNullOrEmpty(groupId))
                return false;

            string mapId = GameWorldHandler.GetCurrentMapName();
            List<BotMemoryStore.DangerZone> zones = BotMemoryStore.GetZonesForMap(mapId);

            for (int i = 0; i < zones.Count; i++)
            {
                Vector3 zonePos = zones[i].Position;
                float radius = zones[i].Radius;

                if (Vector3.Distance(zonePos, _bot.Position) <= SquadPanicRadius)
                {
                    retreatDir = (_bot.Position - zonePos).normalized;
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Panic Logic

        private void StartPanic(float now, Vector3 retreatDir)
        {
            if (_bot == null) return;

            _isPanicking = true;
            _panicStartTime = now;
            _composureLevel = 0f;

            List<Vector3> path = (_cache?.PathCache != null)
                ? BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, retreatDir, _cache.PathCache)
                : new List<Vector3> { _bot.Position + retreatDir * 8f };

            if (path.Count >= 2)
                _bot.GoToPoint(path[1]);

            string mapId = GameWorldHandler.GetCurrentMapName();
            BotMemoryStore.AddDangerZone(mapId, _bot.Position, DangerTriggerType.Panic, 0.6f);

            _bot.Sprint(true);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
        }

        private void EndPanic(float now)
        {
            if (_bot == null) return;

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

        #region External API

        public float GetComposureLevel()
        {
            return _composureLevel;
        }

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
