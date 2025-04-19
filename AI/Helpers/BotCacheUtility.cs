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
        #region Component Access

        /// <summary>
        /// Tries to get a specific component attached to the bot GameObject.
        /// </summary>
        public static bool TryGet<T>(BotComponentCache cache, out T? result) where T : class
        {
            result = null;
            if (cache == null || cache.gameObject == null || cache.Bot == null)
                return false;

            result = cache.GetComponent<T>();
            return result != null;
        }

        /// <summary>
        /// Attempts to retrieve the panic handler component for a bot.
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
        /// Gets the BotComponentCache from a player reference, if present.
        /// </summary>
        public static BotComponentCache? GetCache(Player player)
        {
            return player != null ? player.GetComponent<BotComponentCache>() : null;
        }

        #endregion

        #region Lookup & Metadata

        /// <summary>
        /// Retrieves the assigned personality profile from a cache.
        /// </summary>
        public static BotPersonalityProfile? GetPersonality(BotComponentCache cache)
        {
            if (cache == null || cache.Bot?.ProfileId == null)
                return null;

            return BotRegistry.Get(cache.Bot.ProfileId);
        }

        /// <summary>
        /// Returns the bot's display name and faction side.
        /// </summary>
        public static string GetBotName(BotComponentCache cache)
        {
            if (cache == null || cache.Bot?.Profile?.Info == null)
                return "Unknown";

            string name = cache.Bot.Profile.Info.Nickname;
            string side = cache.Bot.Profile.Side.ToString();
            return $"{name} ({side})";
        }

        #endregion

        #region Visibility & Position

        /// <summary>
        /// Returns the Transform representing the bot's head position.
        /// </summary>
        public static Transform? Head(BotComponentCache cache)
        {
            if (cache?.Bot?.MainParts != null &&
                cache.Bot.MainParts.TryGetValue(BodyPartType.head, out var headPart))
            {
                return headPart?._transform?.Original;
            }

            return null;
        }

        /// <summary>
        /// Returns the transform the bot uses for forward-facing calculations.
        /// </summary>
        public static Transform? GetLookTransform(BotComponentCache cache)
        {
            return cache?.Bot?.Fireport?.Original;
        }

        #endregion

        #region Query Utilities

        /// <summary>
        /// Returns all currently active bots in the scene with alive BotOwners.
        /// </summary>
        public static IEnumerable<BotComponentCache> AllActiveBots()
        {
            BotComponentCache[] all = Object.FindObjectsOfType<BotComponentCache>();
            for (int i = 0; i < all.Length; i++)
            {
                BotComponentCache cache = all[i];
                if (cache?.Bot != null && !cache.Bot.IsDead)
                    yield return cache;
            }
        }

        #endregion

        #region Human Checks

        /// <summary>
        /// Checks if a given Player is a human-controlled character.
        /// </summary>
        public static bool IsHumanPlayer(Player? player)
        {
            return player != null && !player.IsAI;
        }

        /// <summary>
        /// Checks if a given BotOwner is actually controlled by a human (e.g., Coop/FIKA).
        /// </summary>
        public static bool IsHumanPlayer(BotOwner? bot)
        {
            return bot?.GetPlayer != null && !bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
