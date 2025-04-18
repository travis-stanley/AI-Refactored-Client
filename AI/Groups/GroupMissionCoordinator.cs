#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Missions;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Assigns shared missions to bot squads and individual AI based on map context and personality.
    /// Enables group-level decision-making around loot, combat, and quest objectives.
    /// </summary>
    public static class GroupMissionCoordinator
    {
        #region Fields

        private static readonly Dictionary<string, BotMissionSystem.MissionType> _assignedMissions = new(32);
        private static readonly System.Random _rng = new();
        private static bool _debugLog = true;

        #endregion

        #region Public API

        /// <summary>
        /// Enables or disables debug logging for mission assignment.
        /// </summary>
        public static void EnableDebug(bool enabled)
        {
            _debugLog = enabled;
        }

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

#if UNITY_EDITOR
            if (_debugLog)
                Debug.Log($"[AIRefactored-Mission] Assigned group mission '{mission}' to group {groupId}");
#endif

            return mission;
        }

        /// <summary>
        /// Force-assigns a mission type to a specific group.
        /// </summary>
        public static void ForceMissionForGroup(string groupId, BotMissionSystem.MissionType mission)
        {
            if (!string.IsNullOrEmpty(groupId))
                _assignedMissions[groupId] = mission;
        }

        /// <summary>
        /// Clears all mission assignments.
        /// </summary>
        public static void Reset()
        {
            _assignedMissions.Clear();
        }

        #endregion

        #region Internals

        private static BotMissionSystem.MissionType GetSoloBotMission(BotOwner bot)
        {
            var mission = PickWeightedMission(bot);

#if UNITY_EDITOR
            if (_debugLog)
            {
                string name = bot.Profile?.Info?.Nickname ?? "unknown";
                Debug.Log($"[AIRefactored-Mission] Solo bot {name} assigned mission {mission}");
            }
#endif
            return mission;
        }

        /// <summary>
        /// Selects a mission based on map and bot personality profile.
        /// </summary>
        private static BotMissionSystem.MissionType PickWeightedMission(BotOwner bot)
        {
            string map = Singleton<GameWorld>.Instantiated
                ? Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown"
                : "unknown";

            float loot = 1.0f;
            float fight = 1.0f;
            float quest = 1.0f;

            // === Map Influence ===
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

            // === Personality Influence ===
            var wrapper = bot.GetComponent<AIRefactoredBotOwner>();
            if (wrapper?.PersonalityProfile != null)
            {
                var profile = wrapper.PersonalityProfile;

                loot += profile.Caution * 1.0f;
                quest += profile.Caution * 0.5f;
                fight += profile.AggressionLevel * 1.2f;

                if (profile.IsFrenzied) fight += 1.5f;
                if (profile.IsFearful) loot += 1.0f;
                if (profile.IsCamper) quest += 0.75f;
            }

            float total = loot + fight + quest;
            float roll = (float)_rng.NextDouble() * total;

            if (roll < loot) return BotMissionSystem.MissionType.Loot;
            if (roll < loot + fight) return BotMissionSystem.MissionType.Fight;

            return BotMissionSystem.MissionType.Quest;
        }

        /// <summary>
        /// Validates whether the bot is an AI-controlled NPC.
        /// </summary>
        private static bool IsValidAIBot(BotOwner bot)
        {
            return bot?.GetPlayer?.IsAI == true;
        }

        #endregion
    }
}
