#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Looting;
using BepInEx.Logging;
using EFT;
using UnityEngine;
using AIRefactored.Runtime;
using System;
using MissionType = AIRefactored.AI.Missions.BotMissionController.MissionType;

namespace AIRefactored.AI.Missions.Subsystems
{
    /// <summary>
    /// Dynamically switches bot mission type based on context:
    /// panic, aggression, squad cohesion, loot opportunity, etc.
    /// </summary>
    public sealed class MissionSwitcher
    {
        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly BotPersonalityProfile _profile;
        private readonly BotLootScanner? _lootScanner;
        private readonly BotGroupSyncCoordinator? _group;
        private readonly ManualLogSource _log;

        private float _lastSwitchTime;
        private const float SwitchCooldown = 10f;

        public MissionSwitcher(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _profile = BotRegistry.Get(bot.Profile.Id);
            _lootScanner = cache.LootScanner;
            _group = cache.GroupBehavior?.GroupSync;
            _log = AIRefactoredController.Logger;
        }

        /// <summary>
        /// Evaluates and performs a mission switch if conditions are met.
        /// </summary>
        public void Evaluate(ref MissionType currentMission, float time, Action switchToFight, Action resumeQuesting, Func<bool> isGroupAligned)
        {
            if (time - _lastSwitchTime < SwitchCooldown)
                return;

            // Switch to fight if under fire and aggressive
            if (_bot.Memory?.IsUnderFire == true && _profile.AggressionLevel > 0.6f && currentMission != MissionType.Fight)
            {
                _log.LogInfo($"[MissionSwitcher] {_bot.Profile.Info.Nickname} escalating to Fight (under fire + aggressive)");
                _lastSwitchTime = time;
                currentMission = MissionType.Fight;
                switchToFight.Invoke();
                return;
            }

            // Opportunistically switch to loot if idle and loot-focused
            if (currentMission == MissionType.Quest &&
                _profile.PreferredMission == MissionBias.Loot &&
                _lootScanner?.GetHighestValueLootPoint() != null)
            {
                _log.LogInfo($"[MissionSwitcher] {_bot.Profile.Info.Nickname} switching to Loot (loot point nearby)");
                _lastSwitchTime = time;
                currentMission = MissionType.Loot;
                return;
            }

            // If squad scattered, fallback to quest pathing
            if (currentMission == MissionType.Fight && !isGroupAligned())
            {
                _log.LogInfo($"[MissionSwitcher] {_bot.Profile.Info.Nickname} falling back to Quest (squad separation)");
                _lastSwitchTime = time;
                currentMission = MissionType.Quest;
                resumeQuesting.Invoke();
            }
        }
    }
}
