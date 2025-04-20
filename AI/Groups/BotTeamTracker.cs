#nullable enable

using EFT;
using System.Collections.Generic;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Centralized runtime registry for tracking bot squads by GroupId.
    /// Enables coordination, fallback sync, and squad-wide queries.
    /// </summary>
    public static class BotTeamTracker
    {
        #region Internal State

        private static readonly Dictionary<string, List<BotOwner>> _groups = new(32);

        #endregion

        #region Public API

        /// <summary>
        /// Registers a bot into the specified group by ID.
        /// Skips human players or invalid instances.
        /// </summary>
        public static void Register(string groupId, BotOwner bot)
        {
            if (string.IsNullOrEmpty(groupId) || bot == null)
                return;

            var player = bot.GetPlayer;
            if (player == null || !player.IsAI)
                return;

            if (!_groups.TryGetValue(groupId, out var list))
            {
                list = new List<BotOwner>(8);
                _groups[groupId] = list;
            }

            if (!list.Contains(bot))
                list.Add(bot);
        }

        /// <summary>
        /// Registers a bot using its profile’s embedded GroupId (if present).
        /// Safe for calling during BotBrain startup or during bot spawn.
        /// </summary>
        public static void RegisterFromBot(BotOwner bot)
        {
            var groupId = bot?.GetPlayer?.Profile?.Info?.GroupId;
            if (!string.IsNullOrEmpty(groupId))
                Register(groupId!, bot!);
        }

        /// <summary>
        /// Returns a fresh copy of all living squadmates by GroupId.
        /// </summary>
        public static List<BotOwner> GetGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return new List<BotOwner>(0);

            if (!_groups.TryGetValue(groupId, out var original))
                return new List<BotOwner>(0);

            var result = new List<BotOwner>(original.Count);
            for (int i = 0; i < original.Count; i++)
            {
                var bot = original[i];
                if (bot?.GetPlayer?.IsAI == true && !bot.IsDead)
                    result.Add(bot);
            }

            return result;
        }

        /// <summary>
        /// Unregisters the specified bot from all groups.
        /// </summary>
        public static void Unregister(BotOwner bot)
        {
            if (bot == null)
                return;

            foreach (var kvp in _groups)
            {
                var list = kvp.Value;
                if (list.Remove(bot))
                {
                    // Remove the group if it becomes empty after removal
                    if (list.Count == 0)
                        _groups.Remove(kvp.Key);
                    break;
                }
            }
        }

        /// <summary>
        /// Clears all groups. Used on shutdown or full reset.
        /// </summary>
        public static void Clear()
        {
            _groups.Clear();
        }

        /// <summary>
        /// Returns a shallow copy of all group references.
        /// </summary>
        public static Dictionary<string, List<BotOwner>> GetAllGroups()
        {
            return new Dictionary<string, List<BotOwner>>(_groups);
        }

        #endregion
    }
}
