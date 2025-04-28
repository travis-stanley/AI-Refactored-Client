#nullable enable

namespace AIRefactored.AI.Missions.Subsystems
{
    using System;
    using System.Collections.Generic;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Optimization;
    using AIRefactored.Core;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using EFT;
    using EFT.Interactive;
    using EFT.InventoryLogic;

    using UnityEngine;

    /// <summary>
    ///     Evaluates combat, squad alignment, loot threshold, and stuck logic
    ///     to guide mission reassessment and fallback behavior.
    /// </summary>
    public sealed class MissionEvaluator
    {
        private const int LootItemCountThreshold = 40;

        private const float SquadCohesionRange = 10f;

        private const float StuckCooldown = 30f;

        private const float StuckDuration = 25f;

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        private readonly BotOwner _bot;

        private readonly BotComponentCache _cache;

        private readonly BotGroupSyncCoordinator? _group;

        private readonly BotPersonalityProfile _profile;

        private int _fallbackAttempts;

        private float _lastMoveTime;

        private Vector3 _lastPos;

        private float _stuckSince;

        public MissionEvaluator(BotOwner bot, BotComponentCache cache)
        {
            this._bot = bot;
            this._cache = cache;
            this._profile = BotRegistry.Get(bot.Profile.Id);
            this._group = BotCacheUtility.GetGroupSync(cache);
            this._lastPos = bot.Position;
            this._lastMoveTime = Time.time;
        }

        public bool IsGroupAligned()
        {
            if (this._group == null)
                return true;

            var teammates = new List<BotOwner>(this._group.GetTeammates());
            var near = 0;

            for (var i = 0; i < teammates.Count; i++)
            {
                var mate = teammates[i];
                if (mate != null && Vector3.Distance(mate.Position, this._bot.Position) < SquadCohesionRange)
                    near++;
            }

            var required = Mathf.CeilToInt(teammates.Count * 0.6f);
            return near >= required;
        }

        public bool ShouldExtractEarly()
        {
            var threshold = this._profile.RetreatThreshold;
            var backpack = this._bot.GetPlayer?.Inventory?.Equipment?.GetSlot(EquipmentSlot.Backpack)?.ContainedItem;
            if (backpack == null)
                return false;

            var count = 0;
            var items = new List<Item>(backpack.GetAllItems());
            for (var i = 0; i < items.Count; i++)
                if (items[i] != null)
                    count++;

            var fullness = (float)count / LootItemCountThreshold;
            return fullness >= threshold && this._profile.Caution > 0.6f && !this._profile.IsFrenzied;
        }

        public void TryExtract()
        {
            if (this._bot == null || this._bot.IsDead)
                return;

            try
            {
                ExfiltrationPoint? closest = null;
                var minDist = float.MaxValue;

                var allPoints = GameObject.FindObjectsOfType<ExfiltrationPoint>();
                for (var i = 0; i < allPoints.Length; i++)
                {
                    var point = allPoints[i];
                    if (point == null || point.Status != EExfiltrationStatus.RegularMode)
                        continue;

                    var dist = Vector3.Distance(this._bot.Position, point.transform.position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = point;
                    }
                }

                if (closest != null)
                {
                    BotMovementHelper.SmoothMoveTo(this._bot, closest.transform.position);
                    this.Say(EPhraseTrigger.ExitLocated);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[MissionEvaluator] Extraction failed: {ex.Message}");
            }
        }

        public void UpdateStuckCheck(float time)
        {
            var moved = Vector3.Distance(this._bot.Position, this._lastPos);
            if (moved > 0.3f)
            {
                this._lastPos = this._bot.Position;
                this._lastMoveTime = time;
                this._fallbackAttempts = 0;
                return;
            }

            if (time - this._lastMoveTime > StuckDuration && time - this._stuckSince > StuckCooldown
                                                          && this._fallbackAttempts < 2)
            {
                this._stuckSince = time;
                this._fallbackAttempts++;

                var fallback = HybridFallbackResolver.GetBestRetreatPoint(this._bot, this._bot.LookDirection);
                if (fallback.HasValue)
                {
                    _log.LogInfo(
                        $"[MissionEvaluator] {this._bot.Profile.Info.Nickname} fallback #{this._fallbackAttempts} → {fallback.Value}");
                    BotMovementHelper.SmoothMoveTo(this._bot, fallback.Value);
                }
            }
        }

        private void Say(EPhraseTrigger phrase)
        {
            try
            {
                if (!FikaHeadlessDetector.IsHeadless) this._bot.GetPlayer?.Say(phrase);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[MissionEvaluator] VO failed: {ex.Message}");
            }
        }
    }
}