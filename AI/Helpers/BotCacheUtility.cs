#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility methods for safely accessing bot components and AI-related metadata.
    /// </summary>
    public static class BotCacheUtility
    {
        // Central registry for pure-C# cache lookups
        private static readonly Dictionary<BotOwner, BotComponentCache> CacheRegistry = new();

        #region Registry

        public static void Register(BotOwner bot, BotComponentCache cache)
        {
            if (bot != null && !CacheRegistry.ContainsKey(bot))
                CacheRegistry.Add(bot, cache);
        }

        public static void Unregister(BotOwner bot)
        {
            if (bot != null)
                CacheRegistry.Remove(bot);
        }

        public static BotComponentCache? GetCache(BotOwner bot)
        {
            return CacheRegistry.TryGetValue(bot, out var cache) ? cache : null;
        }

        public static BotComponentCache? GetCache(Player player)
        {
            return player?.AIData?.BotOwner is BotOwner bot ? GetCache(bot) : null;
        }

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

        public static BotPersonalityProfile? GetPersonality(BotComponentCache cache)
        {
            if (cache == null || cache.Bot?.ProfileId == null)
                return null;

            return BotRegistry.Get(cache.Bot.ProfileId);
        }

        public static string GetBotName(BotComponentCache cache)
        {
            if (cache == null || cache.Bot?.Profile?.Info == null)
                return "Unknown";

            string name = cache.Bot.Profile.Info.Nickname;
            string side = cache.Bot.Profile.Side.ToString();
            return $"{name} ({side})";
        }

        #endregion

        #region Tactical & Transform

        public static Transform? Head(BotComponentCache cache)
        {
            if (cache?.Bot?.MainParts != null &&
                cache.Bot.MainParts.TryGetValue(BodyPartType.head, out var headPart))
            {
                return headPart?._transform?.Original;
            }

            return null;
        }

        public static Transform? GetLookTransform(BotComponentCache cache)
        {
            return cache?.Bot?.Fireport?.Original;
        }

        #endregion

        #region Panic Support

        public static bool TryGetPanicComponent(BotComponentCache cache, out BotPanicHandler? panic)
        {
            panic = cache.PanicHandler;
            return panic != null;
        }

        #endregion

        #region Human Checks

        public static bool IsHumanPlayer(Player? player)
        {
            return player != null && !player.IsAI;
        }

        public static bool IsHumanPlayer(BotOwner? bot)
        {
            return bot?.GetPlayer != null && !bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
