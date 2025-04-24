#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Centralized utility for managing and querying bot component caches and related AI metadata.
    /// Used to streamline access, avoid reflection, and coordinate bot-wide logic.
    /// </summary>
    public static class BotCacheUtility
    {
        #region Registry

        private static readonly Dictionary<BotOwner, BotComponentCache> CacheRegistry = new(64);

        /// <summary>
        /// Registers a bot and its cache into the global registry.
        /// </summary>
        public static void Register(BotOwner bot, BotComponentCache cache)
        {
            if (bot != null && !CacheRegistry.ContainsKey(bot))
                CacheRegistry[bot] = cache;
        }

        /// <summary>
        /// Unregisters a bot from the registry.
        /// </summary>
        public static void Unregister(BotOwner bot)
        {
            if (bot != null)
                CacheRegistry.Remove(bot);
        }

        /// <summary>
        /// Attempts to retrieve the cache for a given bot.
        /// </summary>
        public static BotComponentCache? GetCache(BotOwner bot)
        {
            return bot != null && CacheRegistry.TryGetValue(bot, out var cache)
                ? cache
                : null;
        }

        /// <summary>
        /// Attempts to retrieve the cache from a Player instance.
        /// </summary>
        public static BotComponentCache? GetCache(Player player)
        {
            return player?.AIData?.BotOwner is BotOwner bot
                ? GetCache(bot)
                : null;
        }

        /// <summary>
        /// Returns all alive bots currently registered in the system.
        /// </summary>
        public static IEnumerable<BotComponentCache> AllActiveBots()
        {
            foreach (var pair in CacheRegistry)
            {
                if (pair.Key != null && !pair.Key.IsDead)
                    yield return pair.Value;
            }
        }

        /// <summary>
        /// Returns the closest bot (non-dead) to a given position within range.
        /// </summary>
        public static BotComponentCache? GetClosestBot(Vector3 origin, float maxDistance = 40f)
        {
            BotComponentCache? closest = null;
            float minDistSq = maxDistance * maxDistance;

            foreach (var pair in CacheRegistry)
            {
                var bot = pair.Key;
                if (bot == null || bot.IsDead) continue;

                float distSq = (bot.Position - origin).sqrMagnitude;
                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    closest = pair.Value;
                }
            }

            return closest;
        }

        #endregion

        #region Personality Metadata

        /// <summary>
        /// Returns the bot's registered personality profile (null if not found).
        /// </summary>
        public static BotPersonalityProfile? GetPersonality(BotComponentCache cache)
        {
            return cache?.Bot?.ProfileId != null
                ? BotRegistry.Get(cache.Bot.ProfileId)
                : null;
        }

        /// <summary>
        /// Formats a debug-friendly bot name + side (e.g., "Gluhar (Savage)").
        /// </summary>
        /// <summary>
        /// Returns a readable bot name with faction. Falls back to "Unknown" if invalid.
        /// </summary>
        /// <param name="cache">Bot component cache containing identity and profile info.</param>
        /// <returns>Formatted bot nickname with side, or "Unknown".</returns>
        public static string GetBotName(BotComponentCache? cache)
        {
            if (cache == null)
                return "Unknown";

            var bot = cache.Bot;
            if (bot == null || bot.Profile == null || bot.Profile.Info == null)
                return "Unknown";

            var info = bot.Profile.Info;
            return $"{info.Nickname} ({bot.Profile.Side})";
        }


        #endregion

        #region Transforms

        public static Transform? Head(BotComponentCache cache)
        {
            if (cache?.Bot?.MainParts == null)
                return null;

            return cache.Bot.MainParts.TryGetValue(BodyPartType.head, out var part)
                ? part?._transform?.Original
                : null;
        }

        public static Transform? GetLookTransform(BotComponentCache cache)
        {
            return cache?.Bot?.Fireport?.Original;
        }

        #endregion

        #region Pose Helpers

        public static string GetStance(BotComponentCache cache)
        {
            var pose = cache?.PoseController;
            if (pose == null)
                return "Unknown";

            float poseLevel = pose.GetPoseLevel();

            if (poseLevel < 25f)
                return "Prone";
            if (poseLevel < 75f)
                return "Crouching";
            return "Standing";
        }

        #endregion

        #region Panic + Perception

        public static bool TryGetPanicComponent(BotComponentCache cache, out BotPanicHandler? panic)
        {
            panic = cache?.PanicHandler;
            return panic != null;
        }

        #endregion

        #region Player Type Helpers

        public static bool IsHumanPlayer(Player? player)
        {
            return player != null && !player.IsAI;
        }

        public static bool IsHumanPlayer(BotOwner? bot)
        {
            return bot?.GetPlayer != null && !bot.GetPlayer.IsAI;
        }

        #endregion

        #region Debug Tools

        public static void DumpCache()
        {
            Debug.Log($"[BotCacheUtility] Dumping {CacheRegistry.Count} bot caches:");

            foreach (var kv in CacheRegistry)
            {
                var bot = kv.Key;
                var cache = kv.Value;
                string name = GetBotName(cache);
                Debug.Log($" → {name}, Position={bot.Position}, Alive={!bot.IsDead}");
            }
        }

        #endregion
    }
}
