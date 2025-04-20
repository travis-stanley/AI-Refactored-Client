#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Missions;
using AIRefactored.Runtime;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using System.Collections.Generic;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Assigns shared missions to bot squads and individuals based on map and personality weightings.
    /// Enables consistent squad coordination across Loot, Fight, and Quest missions.
    /// </summary>
    public static class GroupMissionCoordinator
    {
        #region Fields

        private static readonly Dictionary<string, BotMissionSystem.MissionType> _assignedMissions = new(32);
        private static readonly System.Random _rng = new();
        private static bool _debugLog = true;

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Public API

        public static void EnableDebug(bool enabled) => _debugLog = enabled;

        /// <summary>
        /// Returns the assigned mission type for a bot's group. If not already assigned, one is selected and stored.
        /// </summary>
        public static BotMissionSystem.MissionType GetMissionForGroup(BotOwner bot)
        {
            if (!IsValidAIBot(bot))
                return BotMissionSystem.MissionType.Loot;

            string groupId = bot.Profile?.Info?.GroupId ?? string.Empty;

            if (string.IsNullOrEmpty(groupId))
                return GetSoloBotMission(bot);

            if (_assignedMissions.TryGetValue(groupId, out var existing))
                return existing;

            var mission = PickWeightedMission(bot);
            _assignedMissions[groupId] = mission;

            if (_debugLog)
                _log.LogInfo($"[AIRefactored-Mission] Assigned group mission '{mission}' to group {groupId}");

            return mission;
        }

        /// <summary>
        /// Registers and assigns a mission to the bot’s group (safe to call during bot initialization).
        /// </summary>
        public static void RegisterFromBot(BotOwner bot)
        {
            if (!IsValidAIBot(bot))
                return;

            string groupId = bot.Profile?.Info?.GroupId ?? string.Empty;
            if (string.IsNullOrEmpty(groupId) || _assignedMissions.ContainsKey(groupId))
                return;

            var mission = PickWeightedMission(bot);
            _assignedMissions[groupId] = mission;

            if (_debugLog)
                _log.LogInfo($"[AIRefactored-Mission] [AutoRegister] Assigned group mission '{mission}' to group {groupId}");
        }

        /// <summary>
        /// Forces a group to use a specific mission.
        /// </summary>
        public static void ForceMissionForGroup(string groupId, BotMissionSystem.MissionType mission)
        {
            if (!string.IsNullOrEmpty(groupId))
                _assignedMissions[groupId] = mission;
        }

        /// <summary>
        /// Clears all group mission assignments (typically on session reset).
        /// </summary>
        public static void Reset()
        {
            _assignedMissions.Clear();
        }

        #endregion

        #region Internal Helpers

        private static BotMissionSystem.MissionType GetSoloBotMission(BotOwner bot)
        {
            var mission = PickWeightedMission(bot);

            if (_debugLog)
            {
                string name = bot.Profile?.Info?.Nickname ?? "unknown";
                _log.LogInfo($"[AIRefactored-Mission] Solo bot {name} assigned mission {mission}");
            }

            return mission;
        }

        private static BotMissionSystem.MissionType PickWeightedMission(BotOwner bot)
        {
            string map = Singleton<GameWorld>.Instantiated
                ? Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown"
                : "unknown";

            float loot = 1.0f;
            float fight = 1.0f;
            float quest = 1.0f;

            // === Map Weights ===
            switch (map)
            {
                case "factory4_day":
                case "factory4_night": fight += 1.5f; break;
                case "woods": loot += 1.5f; break;
                case "bigmap": quest += 0.75f; fight += 0.25f; break;
                case "interchange": loot += 1.2f; break;
                case "rezervbase": fight += 1.0f; loot += 0.4f; break;
                case "lighthouse": quest += 1.2f; loot += 1.0f; break;
                case "shoreline": quest += 1.4f; loot += 0.6f; break;
                case "tarkovstreets": fight += 1.3f; loot += 0.5f; break;
                case "laboratory": fight += 2.0f; break;
                case "sandbox":
                case "sandbox_high":
                case "groundzero": loot += 1.0f; break;
                default: loot += 0.5f; break;
            }

            // === Personality Weights ===
            var wrapper = bot.GetComponent<AIRefactoredBotOwner>();
            if (wrapper?.PersonalityProfile is { } profile)
            {
                loot += profile.Caution * 1.0f;
                quest += profile.Caution * 0.5f;
                fight += profile.AggressionLevel * 1.2f;

                if (profile.IsFrenzied) fight += 1.5f;
                if (profile.IsFearful) loot += 1.0f;
                if (profile.IsCamper) quest += 0.75f;
            }

            float total = loot + fight + quest;
            float roll = (float)_rng.NextDouble() * total;

            return roll < loot
                ? BotMissionSystem.MissionType.Loot
                : roll < (loot + fight)
                    ? BotMissionSystem.MissionType.Fight
                    : BotMissionSystem.MissionType.Quest;
        }

        private static bool IsValidAIBot(BotOwner bot)
        {
            return bot?.GetPlayer?.IsAI == true;
        }

        #endregion
    }
}
