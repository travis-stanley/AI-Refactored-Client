#nullable enable

using System.Collections.Generic;
using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility methods for safely accessing bot components and AI-related metadata.
    /// </summary>
    public static class BotCacheUtility
    {
        #region Component Access

        public static bool TryGet<T>(BotComponentCache cache, out T? result) where T : class
        {
            result = null;
            if (cache?.gameObject == null || cache.Bot == null)
                return false;

            result = cache.GetComponent<T>();
            return result != null;
        }

        public static bool TryGetPanicComponent(BotComponentCache cache, out BotPanicHandler? panic)
        {
            panic = null;
            if (cache == null || cache.gameObject == null)
                return false;

            panic = cache.GetComponent<BotPanicHandler>();
            return panic != null;
        }

        public static BotComponentCache? GetCache(Player player)
        {
            return player?.GetComponent<BotComponentCache>();
        }

        #endregion

        #region Lookup & Metadata

        public static BotPersonalityProfile? GetPersonality(BotComponentCache cache)
        {
            if (cache?.Bot?.ProfileId == null)
                return null;

            return BotRegistry.Get(cache.Bot.ProfileId);
        }

        public static string GetBotName(BotComponentCache cache)
        {
            if (cache?.Bot?.Profile?.Info == null)
                return "Unknown";

            string name = cache.Bot.Profile.Info.Nickname;
            string side = cache.Bot.Profile.Side.ToString();
            return $"{name} ({side})";
        }

        #endregion

        #region Visibility & Position

        public static Transform? Head(BotComponentCache cache)
        {
            if (cache?.Bot?.MainParts?.TryGetValue(BodyPartType.head, out var headPart) == true)
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

        #region Query Utilities

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
