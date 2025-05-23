﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Failures in AIRefactored logic must always trigger safe fallback to EFT base AI.
//   Bulletproof: All failures are locally isolated; never disables itself, never cascades to other systems.
//   Realism Pass: All mission switching logic closely mirrors authentic human priorities and squad behaviors.
// </auto-generated>

using MissionType = AIRefactored.AI.Missions.BotMissionController.MissionType;

namespace AIRefactored.AI.Missions.Subsystems
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Looting;
    using AIRefactored.Core;
    using AIRefactored.Runtime;
    using BepInEx.Logging;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Dynamically switches bot mission type based on panic, aggression, loot opportunity, or squad separation.
    /// All failures are locally isolated; cannot break or cascade into other systems.
    /// </summary>
    public sealed class MissionSwitcher
    {
        #region Constants

        private const float SwitchCooldown = 10f;

        #endregion

        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly BotGroupSyncCoordinator _group;
        private readonly BotLootDecisionSystem _lootDecision;
        private readonly BotPersonalityProfile _profile;
        private readonly ManualLogSource _log;

        private float _lastSwitchTime;

        #endregion

        #region Constructor

        public MissionSwitcher(BotOwner bot, BotComponentCache cache)
        {
            if (!EFTPlayerUtil.IsValidBotOwner(bot) || cache == null)
                throw new ArgumentException("[MissionSwitcher] Invalid bot or component cache.");

            _bot = bot;
            _cache = cache;
            _profile = BotRegistry.Get(bot.ProfileId);
            _group = BotCacheUtility.GetGroupSync(cache);
            _lootDecision = cache.LootDecisionSystem;
            _log = Plugin.LoggerInstance;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Evaluates the bot's current state and switches mission if appropriate.
        /// Bulletproof: All failures are local and isolated; movement logic must always be routed through validated helpers.
        /// </summary>
        /// <param name="currentMission">The current mission type of the bot.</param>
        /// <param name="time">Current game time.</param>
        /// <param name="switchToFight">Callback to invoke when switching to Fight.</param>
        /// <param name="resumeQuesting">Callback to invoke when switching to Quest.</param>
        /// <param name="isGroupAligned">Callback to check squad cohesion.</param>
        public void Evaluate(
            ref MissionType currentMission,
            float time,
            Action switchToFight,
            Action resumeQuesting,
            Func<bool> isGroupAligned)
        {
            try
            {
                if (_bot == null || _cache == null || _profile == null)
                    return;

                if (_bot.IsDead || _bot.GetPlayer == null || !_bot.GetPlayer.IsAI)
                    return;

                // Enforce cooldown between mission switches
                if (time - _lastSwitchTime < SwitchCooldown)
                    return;

                string name = _bot.Profile?.Info?.Nickname ?? "Unknown";

                // 1. Escalate to Fight if under fire and aggressive (never triggers instant movement)
                if (_bot.Memory != null &&
                    _bot.Memory.IsUnderFire &&
                    _profile.AggressionLevel > 0.6f &&
                    currentMission != MissionType.Fight)
                {
                    currentMission = MissionType.Fight;
                    _lastSwitchTime = time;
                    switchToFight?.Invoke();
                    _log.LogDebug("[MissionSwitcher] " + name + " escalating → Fight (under fire + aggressive)");
                    return;
                }

                // 2. Switch to Loot if personality prefers and loot is present (never issues direct position set)
                if (currentMission == MissionType.Quest &&
                    _profile.PreferredMission == MissionBias.Loot &&
                    _lootDecision != null &&
                    _lootDecision.ShouldLootNow())
                {
                    // Only switch if loot destination is validated via pooled and helper-based systems.
                    Vector3 lootPos = _lootDecision.GetLootDestination();
                    if (lootPos != Vector3.zero)
                    {
                        // Only change state; never trigger direct movement here!
                        currentMission = MissionType.Loot;
                        _lastSwitchTime = time;
                        _log.LogDebug("[MissionSwitcher] " + name + " switching → Loot (loot opportunity nearby)");
                        return;
                    }
                }

                // 3. Return to Quest if group lost (never snaps movement)
                if (currentMission == MissionType.Fight && isGroupAligned != null && !isGroupAligned())
                {
                    currentMission = MissionType.Quest;
                    _lastSwitchTime = time;
                    resumeQuesting?.Invoke();
                    _log.LogDebug("[MissionSwitcher] " + name + " falling back → Quest (squad separation)");
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[MissionSwitcher] Evaluate failed: {ex}");
            }
        }

        #endregion
    }
}
