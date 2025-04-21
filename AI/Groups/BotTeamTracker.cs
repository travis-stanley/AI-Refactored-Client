#nullable enable

using EFT;
using System.Collections.Generic;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Centralized runtime registry for tracking bot squads by GroupId.
    /// Enables coordination, fallback sync, and squad-wide tactical queries.
    /// </summary>
    public static class BotTeamTracker
    {
        #region Fields

        /// <summary>
        /// Internal map of GroupId to list of active BotOwner references.
        /// </summary>
        private static readonly Dictionary<string, List<BotOwner>> _groups = new Dictionary<string, List<BotOwner>>(32);

        #endregion

        #region Registration

        /// <summary>
        /// Registers a bot to a group by its string GroupId.
        /// Skips null, dead, or non-AI players.
        /// </summary>
        /// <param name="groupId">The group identifier (usually from Profile.Info.GroupId).</param>
        /// <param name="bot">The bot to register.</param>
        public static void Register(string groupId, BotOwner bot)
        {
            if (string.IsNullOrEmpty(groupId) || bot == null)
                return;

            var player = bot.GetPlayer;
            if (player == null || !player.IsAI)
                return;

            if (!_groups.TryGetValue(groupId, out List<BotOwner>? list))
            {
                list = new List<BotOwner>(8);
                _groups[groupId] = list;
            }

            if (!list.Contains(bot))
                list.Add(bot);
        }

        /// <summary>
        /// Automatically registers a bot to its GroupId based on profile metadata.
        /// Safe to call during spawn or AI startup.
        /// </summary>
        /// <param name="bot">The bot to register.</param>
        public static void RegisterFromBot(BotOwner bot)
        {
            if (bot == null)
                return;

            var player = bot.GetPlayer;
            if (player?.Profile?.Info == null)
                return;

            string? groupId = player.Profile.Info.GroupId;
            if (!string.IsNullOrEmpty(groupId))
                Register(groupId, bot);
        }

        #endregion

        #region Querying

        /// <summary>
        /// Returns all valid, living squadmates registered to the given GroupId.
        /// </summary>
        /// <param name="groupId">The group identifier.</param>
        /// <returns>A list of alive AI bots in that group.</returns>
        public static List<BotOwner> GetGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId))
                return new List<BotOwner>(0);

            if (!_groups.TryGetValue(groupId, out List<BotOwner>? original))
                return new List<BotOwner>(0);

            var result = new List<BotOwner>(original.Count);
            for (int i = 0; i < original.Count; i++)
            {
                var bot = original[i];
                if (bot != null && !bot.IsDead && bot.GetPlayer?.IsAI == true)
                {
                    result.Add(bot);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns a shallow copy of the current full group registry.
        /// </summary>
        /// <returns>A dictionary of group keys to bot lists.</returns>
        public static Dictionary<string, List<BotOwner>> GetAllGroups()
        {
            return new Dictionary<string, List<BotOwner>>(_groups);
        }

        #endregion

        #region Cleanup

        /// <summary>
        /// Unregisters the specified bot from any group it is part of.
        /// </summary>
        /// <param name="bot">The bot to remove from registry.</param>
        public static void Unregister(BotOwner bot)
        {
            if (bot == null)
                return;

            string? keyToRemove = null;

            foreach (KeyValuePair<string, List<BotOwner>> kvp in _groups)
            {
                var list = kvp.Value;
                if (list.Remove(bot))
                {
                    if (list.Count == 0)
                        keyToRemove = kvp.Key;
                    break;
                }
            }

            if (keyToRemove != null)
                _groups.Remove(keyToRemove);
        }

        /// <summary>
        /// Wipes all teams from the tracker.
        /// Call this during shutdown or game reset.
        /// </summary>
        public static void Clear()
        {
            _groups.Clear();
        }

        #endregion
    }
}
