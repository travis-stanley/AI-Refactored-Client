#nullable enable

using EFT;
using System.Collections.Generic;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Centralized registry for tracking bot group memberships by GroupId.
    /// Enables squad-based coordination, zone sync, and enemy sharing between squadmates.
    /// </summary>
    public static class BotTeamTracker
    {
        #region Internal Storage

        private static readonly Dictionary<string, List<BotOwner>> _groups = new(32);

        #endregion

        #region Public API

        /// <summary>
        /// Registers a bot into the specified group by GroupId.
        /// Skips human players (FIKA, Coop, or main player).
        /// </summary>
        /// <param name="groupId">The GroupId for the bot squad.</param>
        /// <param name="bot">The bot to register.</param>
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
        /// Retrieves all AI-controlled bots in a group.
        /// </summary>
        /// <param name="groupId">The group ID.</param>
        /// <returns>List of valid AI-controlled BotOwners.</returns>
        public static List<BotOwner> GetGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return new List<BotOwner>(0);

            if (!_groups.TryGetValue(groupId, out var original))
                return new List<BotOwner>(0);

            List<BotOwner> result = new(original.Count);
            for (int i = 0; i < original.Count; i++)
            {
                var bot = original[i];
                if (bot?.GetPlayer?.IsAI == true)
                    result.Add(bot);
            }

            return result;
        }

        /// <summary>
        /// Removes a bot from any group it may belong to.
        /// </summary>
        /// <param name="bot">Bot to unregister.</param>
        public static void Unregister(BotOwner bot)
        {
            if (bot == null)
                return;

            foreach (var kvp in _groups)
            {
                List<BotOwner> list = kvp.Value;
                if (list.Remove(bot))
                {
                    if (list.Count == 0)
                    {
                        _groups.Remove(kvp.Key);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Clears all tracked bot squads.
        /// </summary>
        public static void Clear()
        {
            _groups.Clear();
        }

        /// <summary>
        /// Returns a deep copy of all group data.
        /// </summary>
        public static Dictionary<string, List<BotOwner>> GetAllGroups()
        {
            return new Dictionary<string, List<BotOwner>>(_groups);
        }

        #endregion
    }
}
