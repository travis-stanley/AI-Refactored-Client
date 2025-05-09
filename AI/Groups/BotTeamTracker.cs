﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

#nullable enable

namespace AIRefactored.AI.Groups
{
    using System.Collections.Generic;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Tracks and manages squads of bots by GroupId for tactical queries and squad-level behaviors.
    /// Used by fallback, VO coordination, and dynamic routing systems.
    /// </summary>
    public static class BotTeamTracker
    {
        #region Fields

        private static readonly Dictionary<string, List<BotOwner>> Groups = new Dictionary<string, List<BotOwner>>(32);
        private static readonly List<BotOwner> TempResult = new List<BotOwner>(8);

        #endregion

        #region Public API

        /// <summary>
        /// Clears all registered group states. Call this on session reset.
        /// </summary>
        public static void Clear()
        {
            Groups.Clear();
        }

        /// <summary>
        /// Returns a shallow copy of all registered groups.
        /// Useful for debug overlays or external tools.
        /// </summary>
        public static Dictionary<string, List<BotOwner>> GetAllGroups()
        {
            return new Dictionary<string, List<BotOwner>>(Groups);
        }

        /// <summary>
        /// Retrieves all living AI bots in the specified group.
        /// Returns a defensive new list.
        /// </summary>
        /// <param name="groupId">Group identifier.</param>
        /// <returns>List of BotOwner entries in group.</returns>
        public static List<BotOwner> GetGroup(string groupId)
        {
            TempResult.Clear();

            if (Groups.TryGetValue(groupId, out List<BotOwner>? group))
            {
                for (int i = 0; i < group.Count; i++)
                {
                    BotOwner bot = group[i];
                    if (bot != null && !bot.IsDead && bot.GetPlayer?.IsAI == true)
                    {
                        TempResult.Add(bot);
                    }
                }
            }

            return new List<BotOwner>(TempResult);
        }

        /// <summary>
        /// Prints all registered groups and their sizes to Unity console.
        /// </summary>
        public static void PrintGroups()
        {
            foreach (KeyValuePair<string, List<BotOwner>> entry in Groups)
            {
                Debug.Log($"[BotTeamTracker] Group '{entry.Key}': {entry.Value.Count} member(s)");
            }
        }

        /// <summary>
        /// Registers a bot into a specified group ID.
        /// </summary>
        /// <param name="groupId">The group ID.</param>
        /// <param name="bot">The bot to register.</param>
        public static void Register(string groupId, BotOwner bot)
        {
            if (string.IsNullOrWhiteSpace(groupId) || bot == null || bot.IsDead)
            {
                return;
            }

            Player? player = bot.GetPlayer;
            if (player == null || !player.IsAI)
            {
                return;
            }

            if (!Groups.TryGetValue(groupId, out List<BotOwner>? list))
            {
                list = new List<BotOwner>(4);
                Groups[groupId] = list;
            }

            if (!list.Contains(bot))
            {
                list.Add(bot);
            }
        }

        /// <summary>
        /// Registers a bot automatically based on its profile's GroupId.
        /// </summary>
        /// <param name="bot">The bot to register.</param>
        public static void RegisterFromBot(BotOwner? bot)
        {
            if (bot == null || bot.IsDead)
            {
                return;
            }

            Player? player = bot.GetPlayer;
            if (player == null || player.Profile == null || player.Profile.Info == null)
            {
                return;
            }

            string groupId = player.Profile.Info.GroupId;
            if (!string.IsNullOrWhiteSpace(groupId))
            {
                Register(groupId, bot);
            }
        }

        /// <summary>
        /// Unregisters a bot from any tracked group.
        /// Automatically removes empty groups.
        /// </summary>
        /// <param name="bot">The bot to unregister.</param>
        public static void Unregister(BotOwner? bot)
        {
            if (bot == null)
            {
                return;
            }

            string? toDelete = null;

            foreach (KeyValuePair<string, List<BotOwner>> kvp in Groups)
            {
                List<BotOwner> list = kvp.Value;
                if (list.Remove(bot))
                {
                    if (list.Count == 0)
                    {
                        toDelete = kvp.Key;
                    }

                    break;
                }
            }

            if (toDelete != null)
            {
                Groups.Remove(toDelete);
            }
        }

        #endregion
    }
}
