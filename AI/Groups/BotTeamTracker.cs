#nullable enable

using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Tracks active squads of bots by GroupId for coordination and tactical queries.
    /// Used for routing, voice communication, fallback logic, and group-wide actions.
    /// </summary>
    public static class BotTeamTracker
    {
        #region Internal State

        private static readonly Dictionary<string, List<BotOwner>> _groups = new Dictionary<string, List<BotOwner>>(32);
        private static readonly List<BotOwner> _tempResult = new List<BotOwner>(8);

        #endregion

        #region Registration

        /// <summary>
        /// Registers a bot into the given squad by group ID.
        /// Ignores invalid or dead entries.
        /// </summary>
        public static void Register(string groupId, BotOwner bot)
        {
            if (string.IsNullOrWhiteSpace(groupId) || bot == null)
                return;

            var player = bot.GetPlayer;
            if (player == null || !player.IsAI || bot.IsDead)
                return;

            if (!_groups.TryGetValue(groupId, out var list))
            {
                list = new List<BotOwner>(4);
                _groups[groupId] = list;
            }

            if (!list.Contains(bot))
                list.Add(bot);
        }

        /// <summary>
        /// Convenience helper to register from a bot’s profile GroupId.
        /// </summary>
        /// <summary>
        /// Registers a bot to its group using its profile GroupId, if valid.
        /// </summary>
        /// <param name="bot">The bot to register.</param>
        public static void RegisterFromBot(BotOwner? bot)
        {
            if (bot == null)
                return;

            var player = bot.GetPlayer;
            if (player == null)
                return;

            var profile = player.Profile;
            if (profile == null || profile.Info == null)
                return;

            var info = profile.Info;
            if (!string.IsNullOrEmpty(info.GroupId))
            {
                Register(info.GroupId, bot);
            }
        }


        /// <summary>
        /// Unregisters a bot from its assigned group. Cleans up the group if it's empty.
        /// </summary>
        /// <param name="bot">The bot to unregister.</param>
        public static void Unregister(BotOwner? bot)
        {
            if (bot == null)
                return;

            string? toDelete = null;

            foreach (var kv in _groups)
            {
                List<BotOwner> list = kv.Value;
                if (list.Remove(bot))
                {
                    if (list.Count == 0)
                        toDelete = kv.Key;

                    break;
                }
            }

            if (toDelete != null)
            {
                _groups.Remove(toDelete);
            }
        }


        /// <summary>
        /// Clears all group state. Should be called on session reset.
        /// </summary>
        public static void Clear()
        {
            _groups.Clear();
        }

        #endregion

        #region Query

        /// <summary>
        /// Returns all *living AI* bots in the given group.
        /// Returns a new list for safety.
        /// </summary>
        public static List<BotOwner> GetGroup(string groupId)
        {
            _tempResult.Clear();

            if (_groups.TryGetValue(groupId, out var group))
            {
                for (int i = 0; i < group.Count; i++)
                {
                    var bot = group[i];
                    if (bot == null || bot.IsDead)
                        continue;

                    var player = bot.GetPlayer;
                    if (player?.IsAI == true)
                        _tempResult.Add(bot);
                }
            }

            return new List<BotOwner>(_tempResult);
        }

        /// <summary>
        /// Returns a shallow copy of the current group registry.
        /// Used for debug tools and overview maps.
        /// </summary>
        public static Dictionary<string, List<BotOwner>> GetAllGroups()
        {
            return new Dictionary<string, List<BotOwner>>(_groups);
        }

        #endregion

        #region Debug

        /// <summary>
        /// Prints all groups and their sizes to the Unity console.
        /// </summary>
        public static void PrintGroups()
        {
            foreach (var kv in _groups)
            {
                Debug.Log($"[BotTeamTracker] Group '{kv.Key}': {kv.Value.Count} member(s)");
            }
        }

        #endregion
    }
}
