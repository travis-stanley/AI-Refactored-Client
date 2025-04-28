#nullable enable

namespace AIRefactored.AI.Groups
{
    using System.Collections.Generic;

    using AIRefactored.AI.Missions;
    using AIRefactored.Core;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Assigns and caches group missions for squads and solo bots.
    ///     Mission types are weighted based on map, personality, and combat role.
    /// </summary>
    public static class GroupMissionCoordinator
    {
        private static readonly Dictionary<string, BotMissionController.MissionType> AssignedMissions = new(32);

        /// <summary>
        ///     Force-assigns a specific mission type to a group ID.
        /// </summary>
        public static void ForceMissionForGroup(string groupId, BotMissionController.MissionType mission)
        {
            if (!string.IsNullOrEmpty(groupId))
                AssignedMissions[groupId] = mission;
        }

        /// <summary>
        ///     Gets the mission assigned to this bot’s group.
        ///     Falls back to solo assignment if group is invalid or absent.
        /// </summary>
        public static BotMissionController.MissionType GetMissionForGroup(BotOwner? bot)
        {
            if (bot?.GetPlayer == null || !bot.GetPlayer.IsAI)
                return BotMissionController.MissionType.Loot;

            var profile = bot.Profile;
            if (profile?.Info == null)
                return BotMissionController.MissionType.Loot;

            var groupId = profile.Info.GroupId;
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
        ///     Registers the group mission for this bot’s group, if unassigned.
        /// </summary>
        public static void RegisterFromBot(BotOwner? bot)
        {
            if (bot?.GetPlayer == null || !bot.GetPlayer.IsAI)
                return;

            var profile = bot.Profile;
            if (profile?.Info == null)
                return;

            var groupId = profile.Info.GroupId;
            if (!string.IsNullOrEmpty(groupId) && !AssignedMissions.ContainsKey(groupId))
                AssignedMissions[groupId] = PickMission(bot);
        }

        /// <summary>
        ///     Clears all group mission data. Use on session reset or map change.
        /// </summary>
        public static void Reset()
        {
            AssignedMissions.Clear();
        }

        private static BotMissionController.MissionType PickMission(BotOwner bot)
        {
            float loot = 1f, fight = 1f, quest = 1f;

            var map = GameWorldHandler.GetCurrentMapName()?.ToLowerInvariant() ?? "unknown";

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

            var total = loot + fight + quest;
            var roll = Random.value * total;

            if (roll < loot)
                return BotMissionController.MissionType.Loot;
            if (roll < loot + fight)
                return BotMissionController.MissionType.Fight;

            return BotMissionController.MissionType.Quest;
        }
    }
}