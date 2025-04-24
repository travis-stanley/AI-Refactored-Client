#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using AIRefactored.Core;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using EFT.Interactive;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AIRefactored.AI.Groups;
using AIRefactored.Runtime;
using MissionType = AIRefactored.AI.Missions.BotMissionController.MissionType;

namespace AIRefactored.AI.Missions.Subsystems
{
    /// <summary>
    /// Evaluates combat, squad alignment, loot threshold, and stuck logic
    /// to guide mission reassessment and fallback behavior.
    /// </summary>
    public sealed class MissionEvaluator
    {
        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly BotPersonalityProfile _profile;
        private readonly BotGroupSyncCoordinator? _group;

        private Vector3 _lastPos;
        private float _lastMoveTime;
        private float _stuckSince;
        private int _fallbackAttempts;

        private const float StuckDuration = 25f;
        private const float StuckCooldown = 30f;
        private const int LootItemCountThreshold = 40;
        private const float SquadCohesionRange = 10f;
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        public MissionEvaluator(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _profile = BotRegistry.Get(bot.Profile.Id);
            _group = cache.GroupBehavior?.GroupSync;
            _lastPos = bot.Position;
            _lastMoveTime = Time.time;
        }

        public void UpdateStuckCheck(float time)
        {
            float moved = Vector3.Distance(_bot.Position, _lastPos);
            if (moved > 0.3f)
            {
                _lastPos = _bot.Position;
                _lastMoveTime = time;
                _fallbackAttempts = 0;
                return;
            }

            if (time - _lastMoveTime > StuckDuration && time - _stuckSince > StuckCooldown && _fallbackAttempts < 2)
            {
                _stuckSince = time;
                _fallbackAttempts++;

                Vector3? fallback = HybridFallbackResolver.GetBestRetreatPoint(_bot, _bot.LookDirection);
                if (fallback.HasValue)
                {
                    _log.LogInfo($"[MissionEvaluator] {_bot.Profile.Info.Nickname} fallback #{_fallbackAttempts} → {fallback.Value}");
                    BotMovementHelper.SmoothMoveTo(_bot, fallback.Value);
                }
            }
        }

        public bool ShouldExtractEarly()
        {
            float threshold = _profile.RetreatThreshold;
            var backpack = _bot.GetPlayer?.Inventory?.Equipment?.GetSlot(EquipmentSlot.Backpack)?.ContainedItem;
            if (backpack == null) return false;

            int count = 0;
            foreach (var item in backpack.GetAllItems())
                if (item != null) count++;

            float fullness = (float)count / LootItemCountThreshold;
            return fullness >= threshold && _profile.Caution > 0.6f && !_profile.IsFrenzied;
        }

        public bool IsGroupAligned()
        {
            if (_group == null)
                return true;

            var mates = _group.GetTeammates();
            int near = mates.Count(m => m != null && Vector3.Distance(m.Position, _bot.Position) < SquadCohesionRange);
            return near >= Mathf.CeilToInt(mates.Count * 0.6f);
        }

        public void TryExtract()
        {
            if (_bot == null || _bot.IsDead)
                return;

            try
            {
                ExfiltrationPoint? closest = null;
                float minDist = float.MaxValue;

                foreach (var point in GameObject.FindObjectsOfType<ExfiltrationPoint>())
                {
                    if (point.Status != EExfiltrationStatus.RegularMode) continue;

                    float dist = Vector3.Distance(_bot.Position, point.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = point;
                    }
                }

                if (closest != null)
                {
                    BotMovementHelper.SmoothMoveTo(_bot, closest.transform.position);
                    Say(EPhraseTrigger.ExitLocated);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[MissionEvaluator] Extraction failed: {ex.Message}");
            }
        }

        private void Say(EPhraseTrigger phrase)
        {
            try
            {
                if (!FikaHeadlessDetector.IsHeadless)
                    _bot.GetPlayer?.Say(phrase);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[MissionEvaluator] VO failed: {ex.Message}");
            }
        }
    }
}
