﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Failures in AIRefactored logic must always trigger safe fallback to EFT base AI.
// </auto-generated>

namespace AIRefactored.AI.Medical
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.Core;
    using BepInEx.Logging;
    using EFT;
    using EFT.HealthSystem;
    using UnityEngine;

    /// <summary>
    /// Coordinates healing support across squadmates.
    /// Detects injured allies and applies realistic support actions such as stim packs, aid drops, or voiced reassurances.
    /// All errors are locally isolated; medical logic cannot break the rest of the bot or mod.
    /// </summary>
    public sealed class BotGroupHealCoordinator
    {
        #region Constants

        private const float HealCheckInterval = 3.5f;
        private const float HealthThreshold = 0.6f;
        private const float HealTriggerRange = 10f;
        private static readonly float HealTriggerRangeSqr = HealTriggerRange * HealTriggerRange;

        #endregion

        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private float _nextCheckTime;

        private static readonly EBodyPart[] BodyParts = (EBodyPart[])Enum.GetValues(typeof(EBodyPart));
        private static readonly ManualLogSource Logger = Plugin.LoggerInstance;
        private bool _isActive = true;

        #endregion

        #region Constructor

        public BotGroupHealCoordinator(BotComponentCache cache)
        {
            if (cache == null || cache.Bot == null)
            {
                _isActive = false;
                Logger.LogError("[BotGroupHealCoordinator] Initialization failed: cache or bot is null. Disabling group heal logic for this bot.");
                return;
            }

            _cache = cache;
            _bot = cache.Bot;
            _isActive = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Ticks healing coordination logic and checks squadmates for aid triggers.
        /// Only triggers aid when not in combat and squadmate is genuinely in need.
        /// </summary>
        /// <param name="time">Current game time.</param>
        public void Tick(float time)
        {
            if (!_isActive || _bot == null || _bot.IsDead || _bot.BotsGroup == null || time < _nextCheckTime)
                return;

            try
            {
                _nextCheckTime = time + HealCheckInterval;
                int count = _bot.BotsGroup.MembersCount;

                for (int i = 0; i < count; i++)
                {
                    BotOwner mate = _bot.BotsGroup.Member(i);
                    if (!IsValidMate(mate))
                        continue;

                    Player matePlayer = EFTPlayerUtil.ResolvePlayer(mate);
                    if (!EFTPlayerUtil.IsValidGroupPlayer(matePlayer))
                        continue;

                    // Never attempt heal if mate is fighting
                    if (mate.Memory != null && mate.Memory.GoalEnemy != null)
                        continue;

                    IHealthController health = matePlayer.HealthController;
                    if (health == null || !health.IsAlive || !NeedsHealing(health))
                        continue;

                    // Only heal if we are not panicking or fighting
                    if (_cache.PanicHandler != null && _cache.PanicHandler.IsPanicking)
                        continue;
                    if (_bot.Memory != null && _bot.Memory.GoalEnemy != null)
                        continue;

                    if (_cache.SquadHealer != null && !_cache.SquadHealer.IsInProcess)
                    {
                        IPlayer safeTarget = EFTPlayerUtil.AsSafeIPlayer(matePlayer);
                        _cache.SquadHealer.HealAsk(safeTarget);
                        TrySaySupport(EPhraseTrigger.Cooperation);
                        return;
                    }

                    TrySaySupport(EPhraseTrigger.NeedHelp);
                }
            }
            catch (Exception ex)
            {
                _isActive = false;
                Logger.LogError($"[BotGroupHealCoordinator] Tick() failed: {ex}. Disabling group heal logic for this bot.");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Returns true if mate is nearby, alive, and not self.
        /// </summary>
        private bool IsValidMate(BotOwner mate)
        {
            if (mate == null || mate.IsDead || ReferenceEquals(mate, _bot))
                return false;

            try
            {
                Player selfPlayer = EFTPlayerUtil.ResolvePlayer(_bot);
                Player matePlayer = EFTPlayerUtil.ResolvePlayer(mate);
                if (selfPlayer == null || matePlayer == null)
                    return false;

                Vector3 selfPos = EFTPlayerUtil.GetPosition(selfPlayer);
                Vector3 matePos = EFTPlayerUtil.GetPosition(matePlayer);
                float dx = matePos.x - selfPos.x;
                float dz = matePos.z - selfPos.z;
                float distSqr = (dx * dx) + (dz * dz);

                return distSqr <= HealTriggerRangeSqr;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotGroupHealCoordinator] IsValidMate() failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Returns true if any body part is below threshold health.
        /// </summary>
        private static bool NeedsHealing(IHealthController health)
        {
            try
            {
                for (int i = 0; i < BodyParts.Length; i++)
                {
                    EBodyPart part = BodyParts[i];
                    ValueStruct hp = health.GetBodyPartHealth(part);
                    if (hp.Maximum > 0f && hp.Current < hp.Maximum * HealthThreshold)
                        return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotGroupHealCoordinator] NeedsHealing() failed: {ex}");
            }
            return false;
        }

        /// <summary>
        /// Tries to voice support or healing phrase for realism, unless in headless mode.
        /// </summary>
        private void TrySaySupport(EPhraseTrigger phrase)
        {
            try
            {
                if (!FikaHeadlessDetector.IsHeadless && _bot != null && _bot.BotTalk != null)
                {
                    _bot.BotTalk.TrySay(phrase);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotGroupHealCoordinator] TrySaySupport() failed: {ex}");
            }
        }

        #endregion
    }
}
