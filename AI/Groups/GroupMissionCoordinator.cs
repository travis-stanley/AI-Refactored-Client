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
    /// Assigns shared missions to bot squads and individuals based on map context and personality weighting.
    /// Enables consistent squad-level coordination across Loot, Fight, and Quest behaviors.
    /// </summary>
    public static class GroupMissionCoordinator
    {
        #region Fields

        private static readonly Dictionary<string, BotMissionSystem.MissionType> _assignedMissions = new Dictionary<string, BotMissionSystem.MissionType>(32);
        private static readonly System.Random _rng = new System.Random();
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;
        private static bool _debugLog = true;

        #endregion

        #region Public API

        /// <summary>
        /// Enables or disables mission assignment logging for debugging purposes.
        /// </summary>
        public static void EnableDebug(bool enabled)
        {
            _debugLog = enabled;
        }

        /// <summary>
        /// Gets or assigns a mission to a squad group based on GroupId.
        /// If mission is not yet set, uses weighted logic.
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
        /// Registers a bot’s group for mission assignment (auto-calls PickWeightedMission).
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
        /// Manually assigns a mission to a group, overriding weighted behavior.
        /// </summary>
        public static void ForceMissionForGroup(string groupId, BotMissionSystem.MissionType mission)
        {
            if (!string.IsNullOrEmpty(groupId))
                _assignedMissions[groupId] = mission;
        }

        /// <summary>
        /// Clears all registered group missions. Called on reset or new raid.
        /// </summary>
        public static void Reset()
        {
            _assignedMissions.Clear();
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Assigns a mission to a solo bot using map+personality weight.
        /// </summary>
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

        /// <summary>
        /// Computes weighted probability of Loot, Fight, or Quest mission using map and personality traits.
        /// </summary>
        private static BotMissionSystem.MissionType PickWeightedMission(BotOwner bot)
        {
            string map = "unknown";
            if (Singleton<GameWorld>.Instantiated)
                map = Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown";

            float loot = 1f, fight = 1f, quest = 1f;

            switch (map)
            {
                case "factory4_day":
                case "factory4_night":
                    fight += 1.5f;
                    break;
                case "woods":
                    loot += 1.5f;
                    break;
                case "bigmap":
                    quest += 0.75f; fight += 0.25f;
                    break;
                case "interchange":
                    loot += 1.2f;
                    break;
                case "rezervbase":
                    fight += 1.0f; loot += 0.4f;
                    break;
                case "lighthouse":
                    quest += 1.2f; loot += 1.0f;
                    break;
                case "shoreline":
                    quest += 1.4f; loot += 0.6f;
                    break;
                case "tarkovstreets":
                    fight += 1.3f; loot += 0.5f;
                    break;
                case "laboratory":
                    fight += 2.0f;
                    break;
                case "sandbox":
                case "sandbox_high":
                case "groundzero":
                    loot += 1.0f;
                    break;
                default:
                    loot += 0.5f;
                    break;
            }

            var wrapper = bot.GetComponent<AIRefactoredBotOwner>();
            if (wrapper?.PersonalityProfile is BotPersonalityProfile profile)
            {
                loot += profile.Caution * 1.0f;
                quest += profile.Caution * 0.5f;
                fight += profile.AggressionLevel * 1.2f;

                if (profile.IsFrenzied)
                    fight += 1.5f;
                if (profile.IsFearful)
                    loot += 1.0f;
                if (profile.IsCamper)
                    quest += 0.75f;
            }

            float total = loot + fight + quest;
            float roll = (float)_rng.NextDouble() * total;

            return roll < loot
                ? BotMissionSystem.MissionType.Loot
                : roll < (loot + fight)
                    ? BotMissionSystem.MissionType.Fight
                    : BotMissionSystem.MissionType.Quest;
        }

        /// <summary>
        /// Returns true if the bot is valid, AI-controlled, and not null/dead.
        /// </summary>
        private static bool IsValidAIBot(BotOwner bot)
        {
            return bot != null && bot.GetPlayer?.IsAI == true;
        }

        #endregion
    }
}
