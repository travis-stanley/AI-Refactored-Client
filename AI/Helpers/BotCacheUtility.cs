#nullable enable

using System.Collections.Generic;
using EFT;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utilities for working with BotComponentCache and accessing bot information.
    /// </summary>
    public static class BotCacheUtility
    {
        /// <summary>
        /// Tries to fetch a component of type T from a bot's cache GameObject.
        /// </summary>
        public static bool TryGet<T>(BotComponentCache cache, out T? result) where T : class
        {
            result = null;
            if (cache?.gameObject == null || cache.Bot == null)
                return false;

            result = cache.GetComponent<T>();
            return result != null;
        }

        /// <summary>
        /// Returns all active (alive) bots in the scene.
        /// </summary>
        public static IEnumerable<BotComponentCache> AllActiveBots()
        {
            var results = new List<BotComponentCache>();
            var all = Object.FindObjectsOfType<BotComponentCache>();

            foreach (var cache in all)
            {
                if (cache != null && cache.Bot != null && !cache.Bot.IsDead)
                {
                    results.Add(cache);
                }
            }

            return results;
        }

        /// <summary>
        /// Attempts to return the bot's head transform.
        /// </summary>
        public static Transform? Head(BotComponentCache cache)
        {
            if (cache?.Bot?.MainParts?.TryGetValue(BodyPartType.head, out var headPart) == true)
            {
                return headPart?._transform?.Original;
            }
            return null;
        }

        /// <summary>
        /// Attempts to return the bot's look transform (Fireport).
        /// </summary>
        public static Transform? GetLookTransform(BotComponentCache cache)
        {
            return cache?.Bot?.Fireport?.Original;
        }

        /// <summary>
        /// Tries to resolve the BotPanicHandler component from cache.
        /// </summary>
        public static bool TryGetPanicComponent(BotComponentCache cache, out BotPanicHandler? panic)
        {
            panic = null;
            if (cache == null || cache.gameObject == null)
                return false;

            panic = cache.GetComponent<BotPanicHandler>();
            return panic != null;
        }

        /// <summary>
        /// Returns the visible name for a bot from its cache.
        /// </summary>
        public static string GetBotName(BotComponentCache cache)
        {
            if (cache?.Bot?.Profile?.Info == null)
                return "Unknown";

            string name = cache.Bot.Profile.Info.Nickname;
            string side = cache.Bot.Profile.Side.ToString();
            return $"{name} ({side})";
        }

        /// <summary>
        /// Attempts to resolve the bot's personality from the BotRegistry.
        /// </summary>
        public static BotPersonalityProfile? GetPersonality(BotComponentCache cache)
        {
            if (cache?.Bot?.ProfileId == null)
                return null;

            return BotRegistry.Get(cache.Bot.ProfileId);
        }

        /// <summary>
        /// Gets the BotComponentCache from a player object if available.
        /// </summary>
        public static BotComponentCache? GetCache(Player player)
        {
            return player?.GetComponent<BotComponentCache>();
        }
    }
}
