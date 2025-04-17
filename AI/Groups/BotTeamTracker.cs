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
        public static List<BotOwner> GetGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return new List<BotOwner>();

            if (!_groups.TryGetValue(groupId, out var list))
                return new List<BotOwner>();

            var result = new List<BotOwner>(list.Count);
            foreach (var bot in list)
            {
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
        public static void Unregister(BotOwner bot)
        {
            if (bot == null)
                return;

            foreach (var kvp in _groups)
            {
                if (kvp.Value.Remove(bot))
                {
                    if (kvp.Value.Count == 0)
                    {
                        _groups.Remove(kvp.Key);
                    }
                    break;
                }
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
