#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility methods for safely accessing bot components, transforms, and AI metadata.
    /// </summary>
    public static class BotCacheUtility
    {
        #region Registry

        private static readonly Dictionary<BotOwner, BotComponentCache> CacheRegistry = new Dictionary<BotOwner, BotComponentCache>(64);

        /// <summary>
        /// Registers a bot and its associated cache into the global bot registry.
        /// </summary>
        public static void Register(BotOwner bot, BotComponentCache cache)
        {
            if (bot != null && !CacheRegistry.ContainsKey(bot))
                CacheRegistry.Add(bot, cache);
        }

        /// <summary>
        /// Removes a bot from the global registry.
        /// </summary>
        public static void Unregister(BotOwner bot)
        {
            if (bot != null)
                CacheRegistry.Remove(bot);
        }

        /// <summary>
        /// Gets the BotComponentCache associated with a given BotOwner.
        /// </summary>
        public static BotComponentCache? GetCache(BotOwner bot)
        {
            return bot != null && CacheRegistry.TryGetValue(bot, out var cache) ? cache : null;
        }

        /// <summary>
        /// Gets the BotComponentCache for a given EFT Player if it is AI-controlled.
        /// </summary>
        public static BotComponentCache? GetCache(Player player)
        {
            return player?.AIData?.BotOwner is BotOwner bot ? GetCache(bot) : null;
        }

        /// <summary>
        /// Returns all alive bots currently in the registry.
        /// </summary>
        public static IEnumerable<BotComponentCache> AllActiveBots()
        {
            foreach (var pair in CacheRegistry)
            {
                if (pair.Key != null && !pair.Key.IsDead)
                    yield return pair.Value;
            }
        }

        #endregion

        #region Metadata & Personality

        /// <summary>
        /// Returns the personality profile for a given bot cache if available.
        /// </summary>
        public static BotPersonalityProfile? GetPersonality(BotComponentCache cache)
        {
            if (cache == null || cache.Bot?.ProfileId == null)
                return null;

            return BotRegistry.Get(cache.Bot.ProfileId);
        }

        /// <summary>
        /// Returns a formatted display name for the bot using nickname and side.
        /// </summary>
        public static string GetBotName(BotComponentCache cache)
        {
            if (cache?.Bot?.Profile?.Info == null)
                return "Unknown";

            string name = cache.Bot.Profile.Info.Nickname;
            string side = cache.Bot.Profile.Side.ToString();
            return $"{name} ({side})";
        }

        #endregion

        #region Tactical & Transform Access

        /// <summary>
        /// Returns the world-space head transform for this bot using EFT’s internal EnemyPart.
        /// </summary>
        public static Transform? Head(BotComponentCache cache)
        {
            if (cache?.Bot?.MainParts == null)
                return null;

            EnemyPart? part;
            if (!cache.Bot.MainParts.TryGetValue(BodyPartType.head, out part))
                return null;

            return part?._transform?.Original;
        }

        /// <summary>
        /// Returns the fireport transform used for aiming direction.
        /// </summary>
        public static Transform? GetLookTransform(BotComponentCache cache)
        {
            return cache?.Bot?.Fireport?.Original;
        }

        #endregion

        #region Panic Support

        /// <summary>
        /// Attempts to retrieve the panic handler from the bot’s cache.
        /// </summary>
        public static bool TryGetPanicComponent(BotComponentCache cache, out BotPanicHandler? panic)
        {
            panic = cache?.PanicHandler;
            return panic != null;
        }

        #endregion

        #region Human Player Checks

        /// <summary>
        /// Returns true if the given player is a human (non-AI) player.
        /// </summary>
        public static bool IsHumanPlayer(Player? player)
        {
            return player != null && !player.IsAI;
        }

        /// <summary>
        /// Returns true if the bot represents a human player.
        /// </summary>
        public static bool IsHumanPlayer(BotOwner? bot)
        {
            return bot?.GetPlayer != null && !bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
