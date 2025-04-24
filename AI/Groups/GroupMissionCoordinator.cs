#nullable enable

using AIRefactored.AI.Missions;
using AIRefactored.Core;
using EFT;
using System.Collections.Generic;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Assigns and caches group missions for squads and solo bots.
    /// Mission types are weighted based on map, personality, and combat role.
    /// </summary>
    public static class GroupMissionCoordinator
    {
        #region Fields

        private static readonly Dictionary<string, BotMissionSystem.MissionType> AssignedMissions = new Dictionary<string, BotMissionSystem.MissionType>(32);

        #endregion

        #region Public API

        /// <summary>
        /// Gets the mission assigned to this bot’s group.
        /// Falls back to solo assignment if group is invalid or absent.
        /// </summary>
        public static BotMissionSystem.MissionType GetMissionForGroup(BotOwner? bot)
        {
            if (bot?.GetPlayer == null || !bot.GetPlayer.IsAI)
                return BotMissionSystem.MissionType.Loot;

            var profile = bot.Profile;
            if (profile?.Info == null)
                return BotMissionSystem.MissionType.Loot;

            string groupId = profile.Info.GroupId;
            if (string.IsNullOrEmpty(groupId))
                return PickMission(bot); // solo fallback

            if (!AssignedMissions.TryGetValue(groupId, out var mission))
            {
                mission = PickMission(bot);
                AssignedMissions[groupId] = mission;
            }

            return mission;
        }

        /// <summary>
        /// Registers the group mission for this bot’s group, if unassigned.
        /// </summary>
        public static void RegisterFromBot(BotOwner? bot)
        {
            if (bot?.GetPlayer == null || !bot.GetPlayer.IsAI)
                return;

            var profile = bot.Profile;
            if (profile?.Info == null)
                return;

            string groupId = profile.Info.GroupId;
            if (!string.IsNullOrEmpty(groupId) && !AssignedMissions.ContainsKey(groupId))
            {
                AssignedMissions[groupId] = PickMission(bot);
            }
        }

        /// <summary>
        /// Force-assigns a specific mission type to a group ID.
        /// </summary>
        public static void ForceMissionForGroup(string groupId, BotMissionSystem.MissionType mission)
        {
            if (!string.IsNullOrEmpty(groupId))
                AssignedMissions[groupId] = mission;
        }

        /// <summary>
        /// Clears all group mission data. Use on session reset or map change.
        /// </summary>
        public static void Reset()
        {
            AssignedMissions.Clear();
        }

        #endregion

        #region Internal Logic

        /// <summary>
        /// Dynamically picks a mission based on bot personality and current map.
        /// </summary>
        private static BotMissionSystem.MissionType PickMission(BotOwner bot)
        {
            float loot = 1f, fight = 1f, quest = 1f;

            string map = GameWorldHandler.GetCurrentMapName()?.ToLowerInvariant() ?? "unknown";

            switch (map)
            {
                case "factory4_day":
                case "factory4_night":
                    fight += 1.5f;
                    break;
                case "woods":
                    loot += 1.5f;
                    break;
                case "bigmap": // Customs
                    quest += 0.75f;
                    fight += 0.25f;
                    break;
                case "interchange":
                    loot += 1.2f;
                    break;
                case "rezervbase":
                    fight += 1.0f;
                    loot += 0.4f;
                    break;
                case "lighthouse":
                    quest += 1.2f;
                    loot += 1.0f;
                    break;
                case "shoreline":
                    quest += 1.4f;
                    loot += 0.6f;
                    break;
                case "tarkovstreets":
                    fight += 1.3f;
                    loot += 0.5f;
                    break;
                case "laboratory":
                    fight += 2f;
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

            var personality = BotRegistry.TryGet(bot.ProfileId);
            if (personality != null)
            {
                loot += personality.Caution;
                quest += personality.Caution * 0.5f;
                fight += personality.AggressionLevel * 1.2f;

                if (personality.IsFrenzied) fight += 1.5f;
                if (personality.IsFearful) loot += 1.0f;
                if (personality.IsCamper) quest += 0.75f;
            }

            float total = loot + fight + quest;
            float roll = UnityEngine.Random.value * total;

            if (roll < loot)
                return BotMissionSystem.MissionType.Loot;
            if (roll < loot + fight)
                return BotMissionSystem.MissionType.Fight;

            return BotMissionSystem.MissionType.Quest;
        }

        #endregion
    }
}
