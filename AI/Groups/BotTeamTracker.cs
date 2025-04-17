#nullable enable

using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Centralized registry for tracking bot group memberships by GroupId.
    /// Enables squad-based coordination, zone sync, and enemy sharing between squadmates.
    /// </summary>
    public static class BotTeamTracker
    {
        #region Internal Storage

        private static readonly Dictionary<string, List<BotOwner>> _groups = new();

        #endregion

        #region Public API

        /// <summary>
        /// Registers a bot into the specified group by GroupId.
        /// If the group does not exist, it will be created.
        /// Skips all human players (FIKA or otherwise).
        /// </summary>
        /// <param name="groupId">Unique group identifier (from EFT profile).</param>
        /// <param name="bot">The AI BotOwner instance to register.</param>
        public static void Register(string groupId, BotOwner bot)
        {
            if (string.IsNullOrEmpty(groupId) || bot == null)
                return;

            var player = bot.GetPlayer;
            if (player == null || !player.IsAI)
                return; // ❌ Don't track human players

            if (!_groups.TryGetValue(groupId, out var list))
            {
                list = new List<BotOwner>();
                _groups[groupId] = list;
            }

            if (!list.Contains(bot))
            {
                list.Add(bot);
            }
        }

        /// <summary>
        /// Retrieves all bots in a group, filtering out any human players just in case.
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <returns>List of valid AI BotOwner instances.</returns>
        public static List<BotOwner> GetGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return new List<BotOwner>();

            if (!_groups.TryGetValue(groupId, out var list))
                return new List<BotOwner>();

            var result = new List<BotOwner>(list.Count);
            for (int i = 0; i < list.Count; i++)
            {
                var bot = list[i];
                var player = bot?.GetPlayer;

                if (bot != null && player != null && player.IsAI)
                {
                    result.Add(bot);
                }
            }

            return result;
        }

        /// <summary>
        /// Unregisters a bot from all groups it may belong to.
        /// </summary>
        /// <param name="bot">Bot to remove from tracking.</param>
        public static void Unregister(BotOwner bot)
        {
            if (bot == null)
                return;

            string? targetKey = null;

            foreach (var kvp in _groups)
            {
                if (kvp.Value.Remove(bot))
                {
                    if (kvp.Value.Count == 0)
                        targetKey = kvp.Key;
                    break;
                }
            }

            if (!string.IsNullOrEmpty(targetKey))
            {
                _groups.Remove(targetKey!);
            }
        }

        /// <summary>
        /// Clears all group membership records from the tracker.
        /// </summary>
        public static void Clear()
        {
            _groups.Clear();
        }

        /// <summary>
        /// Returns the full dictionary of currently active bot groups.
        /// </summary>
        public static IReadOnlyDictionary<string, List<BotOwner>> GetAllGroups()
        {
            return _groups;
        }

        #endregion
    }
}
