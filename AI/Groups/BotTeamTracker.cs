#nullable enable

namespace AIRefactored.AI.Groups
{
    using System.Collections.Generic;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Tracks active squads of bots by GroupId for coordination and tactical queries.
    ///     Used for routing, voice communication, fallback logic, and group-wide actions.
    /// </summary>
    public static class BotTeamTracker
    {
        private static readonly Dictionary<string, List<BotOwner>> _groups = new(32);

        private static readonly List<BotOwner> _tempResult = new(8);

        /// <summary>
        ///     Clears all group state. Should be called on session reset.
        /// </summary>
        public static void Clear()
        {
            _groups.Clear();
        }

        /// <summary>
        ///     Returns a shallow copy of the current group registry.
        ///     Used for debug tools and overview maps.
        /// </summary>
        public static Dictionary<string, List<BotOwner>> GetAllGroups()
        {
            return new Dictionary<string, List<BotOwner>>(_groups);
        }

        /// <summary>
        ///     Returns all *living AI* bots in the given group.
        ///     Returns a new list for safety.
        /// </summary>
        public static List<BotOwner> GetGroup(string groupId)
        {
            _tempResult.Clear();

            if (_groups.TryGetValue(groupId, out var group))
                foreach (var bot in group)
                {
                    var player = bot?.GetPlayer;
                    if (bot != null && !bot.IsDead && player?.IsAI == true)
                        _tempResult.Add(bot);
                }

            return new List<BotOwner>(_tempResult);
        }

        /// <summary>
        ///     Prints all groups and their sizes to the Unity console.
        /// </summary>
        public static void PrintGroups()
        {
            foreach (var kv in _groups)
                Debug.Log($"[BotTeamTracker] Group '{kv.Key}': {kv.Value.Count} member(s)");
        }

        /// <summary>
        ///     Registers a bot into the given squad by group ID.
        ///     Ignores invalid or dead entries.
        /// </summary>
        public static void Register(string groupId, BotOwner bot)
        {
            if (string.IsNullOrWhiteSpace(groupId) || bot == null || bot.IsDead)
                return;

            var player = bot.GetPlayer;
            if (player == null || !player.IsAI)
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
        ///     Registers a bot to its group using its profile GroupId, if valid.
        /// </summary>
        public static void RegisterFromBot(BotOwner? bot)
        {
            if (bot == null || bot.IsDead)
                return;

            var player = bot.GetPlayer;
            if (player?.Profile?.Info?.GroupId is string groupId && !string.IsNullOrEmpty(groupId))
                Register(groupId, bot);
        }

        /// <summary>
        ///     Unregisters a bot from its assigned group. Cleans up the group if it's empty.
        /// </summary>
        public static void Unregister(BotOwner? bot)
        {
            if (bot == null)
                return;

            string? toDelete = null;

            foreach (var kv in _groups)
            {
                var list = kv.Value;
                if (list.Remove(bot))
                {
                    if (list.Count == 0)
                        toDelete = kv.Key;
                    break;
                }
            }

            if (toDelete != null)
                _groups.Remove(toDelete);
        }
    }
}