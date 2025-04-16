using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    public static class BotTeamTracker
    {
        private static readonly Dictionary<string, List<BotOwner>> _groups = new();

        /// <summary>
        /// Registers a bot into the specified group.
        /// </summary>
        public static void Register(string groupId, BotOwner bot)
        {
            if (string.IsNullOrEmpty(groupId) || bot == null)
                return;

            if (!_groups.TryGetValue(groupId, out var list))
            {
                list = new List<BotOwner>();
                _groups[groupId] = list;
            }

            if (!list.Contains(bot))
            {
                list.Add(bot);

#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-TeamTracker] Registered {bot.Profile?.Info?.Nickname ?? "unknown"} to group {groupId}");
#endif
            }
        }

        /// <summary>
        /// Gets the list of bots in a specific group.
        /// </summary>
        public static List<BotOwner> GetGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return new List<BotOwner>();

            return _groups.TryGetValue(groupId, out var list) ? list : new List<BotOwner>();
        }

        /// <summary>
        /// Unregisters a bot from all groups it belongs to.
        /// </summary>
        public static void Unregister(BotOwner bot)
        {
            if (bot == null)
                return;

            foreach (var kvp in _groups)
            {
                if (kvp.Value.Remove(bot) && kvp.Value.Count == 0)
                {
                    _groups.Remove(kvp.Key);
                    break;
                }
            }

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored-TeamTracker] Unregistered {bot.Profile?.Info?.Nickname ?? "unknown"}");
#endif
        }

        /// <summary>
        /// Clears all team registrations.
        /// </summary>
        public static void Clear()
        {
            _groups.Clear();
#if UNITY_EDITOR
            Debug.Log("[AIRefactored-TeamTracker] Cleared all bot groups.");
#endif
        }

        /// <summary>
        /// Returns all active groups.
        /// </summary>
        public static IReadOnlyDictionary<string, List<BotOwner>> GetAllGroups()
        {
            return _groups;
        }
    }
}
