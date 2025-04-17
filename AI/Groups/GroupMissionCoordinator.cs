#nullable enable

using System;
using System.Collections.Generic;
using Comfort.Common;
using EFT;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Missions;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Assigns shared missions to bot squads and individual AI, based on map and personality profile.
    /// Supports group-level decision-making for loot, combat, or quest behavior.
    /// </summary>
    public static class GroupMissionCoordinator
    {
        #region Fields

        private static readonly Dictionary<string, BotMissionSystem.MissionType> _assignedMissions = new();
        private static readonly System.Random _rng = new();
        private static bool _debugLog = true;

        #endregion

        #region Public API

        /// <summary>
        /// Enables or disables debug logging at runtime.
        /// </summary>
        public static void EnableDebug(bool enabled)
        {
            _debugLog = enabled;
        }

        /// <summary>
        /// Returns the shared mission type assigned to a bot's group, or assigns a new one.
        /// Skips human players.
        /// </summary>
        public static BotMissionSystem.MissionType GetMissionForGroup(BotOwner bot)
        {
            if (!IsValidAIBot(bot))
                return BotMissionSystem.MissionType.Loot;

            string groupId = bot.Profile?.Info?.GroupId ?? string.Empty;

            if (string.IsNullOrEmpty(groupId))
                return GetSoloBotMission(bot);

            if (_assignedMissions.TryGetValue(groupId, out var mission))
                return mission;

            mission = PickWeightedMission(bot);
            _assignedMissions[groupId] = mission;

#if UNITY_EDITOR
            if (_debugLog)
                Debug.Log($"[AIRefactored-Mission] Assigned group mission '{mission}' to group {groupId}");
#endif
            return mission;
        }

        /// <summary>
        /// Overrides mission type for a specific group manually.
        /// </summary>
        public static void ForceMissionForGroup(string groupId, BotMissionSystem.MissionType mission)
        {
            if (!string.IsNullOrEmpty(groupId))
                _assignedMissions[groupId] = mission;
        }

        /// <summary>
        /// Resets all assigned group missions.
        /// </summary>
        public static void Reset()
        {
            _assignedMissions.Clear();
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Returns a mission for a solo (non-grouped) bot using weighted logic.
        /// </summary>
        private static BotMissionSystem.MissionType GetSoloBotMission(BotOwner bot)
        {
            var mission = PickWeightedMission(bot);

#if UNITY_EDITOR
            if (_debugLog)
                Debug.Log($"[AIRefactored-Mission] Solo bot {bot.Profile?.Info?.Nickname} assigned mission {mission}");
#endif
            return mission;
        }

        /// <summary>
        /// Picks a mission based on current map and bot personality.
        /// </summary>
        private static BotMissionSystem.MissionType PickWeightedMission(BotOwner bot)
        {
            string map = Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown";

            float lootWeight = 1.0f;
            float fightWeight = 1.0f;
            float questWeight = 1.0f;

            // === Map-Based Weights ===
            switch (map)
            {
                case "factory4_day":
                case "factory4_night":
                    fightWeight += 1.5f;
                    break;
                case "woods":
                    lootWeight += 1.5f;
                    break;
                case "bigmap":
                    questWeight += 0.75f;
                    fightWeight += 0.25f;
                    break;
                case "interchange":
                    lootWeight += 1.2f;
                    break;
                case "rezervbase":
                    fightWeight += 1.0f;
                    lootWeight += 0.4f;
                    break;
                case "lighthouse":
                    questWeight += 1.2f;
                    lootWeight += 1.0f;
                    break;
                case "shoreline":
                    questWeight += 1.4f;
                    lootWeight += 0.6f;
                    break;
                case "tarkovstreets":
                    fightWeight += 1.3f;
                    lootWeight += 0.5f;
                    break;
                case "laboratory":
                    fightWeight += 2.0f;
                    break;
                case "sandbox":
                case "sandbox_high":
                case "groundzero":
                    lootWeight += 1.0f;
                    break;
                default:
                    lootWeight += 0.5f;
                    break;
            }

            // === Personality-Based Weights ===
            var profile = bot?.GetComponent<AIRefactoredBotOwner>()?.PersonalityProfile;
            if (profile != null)
            {
                lootWeight += profile.Caution * 1.0f;
                questWeight += profile.Caution * 0.5f;
                fightWeight += profile.AggressionLevel * 1.2f;

                if (profile.IsFrenzied) fightWeight += 1.5f;
                if (profile.IsFearful) lootWeight += 1.0f;
                if (profile.IsCamper) questWeight += 0.75f;
            }

            // === Weighted Random Roll ===
            float total = lootWeight + fightWeight + questWeight;
            float roll = (float)_rng.NextDouble() * total;

            if (roll < lootWeight)
                return BotMissionSystem.MissionType.Loot;

            if (roll < lootWeight + fightWeight)
                return BotMissionSystem.MissionType.Fight;

            return BotMissionSystem.MissionType.Quest;
        }

        /// <summary>
        /// Returns true if this is a valid AI-controlled bot (non-player).
        /// </summary>
        private static bool IsValidAIBot(BotOwner bot)
        {
            return bot?.GetPlayer != null && bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
