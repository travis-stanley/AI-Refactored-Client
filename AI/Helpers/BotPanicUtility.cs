#nullable enable

using System.Collections.Generic;
using UnityEngine;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Core;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Triggers panic behavior in individual or grouped bots.
    /// Used by flashlight detection, suppression, and perception systems.
    /// </summary>
    public static class BotPanicUtility
    {
        #region Component Access

        /// <summary>
        /// Attempts to resolve a BotPanicHandler component from the cache.
        /// </summary>
        public static bool TryGetPanicComponent(BotComponentCache cache, out BotPanicHandler? panic)
        {
            panic = null;
            if (cache == null)
                return false;

            panic = cache.GetComponent<BotPanicHandler>();
            return panic != null;
        }

        /// <summary>
        /// Backward-compatible alias for TryGetPanicComponent.
        /// </summary>
        public static bool TryGet(BotComponentCache cache, out BotPanicHandler? panic)
        {
            return TryGetPanicComponent(cache, out panic);
        }

        #endregion

        #region Panic Triggers

        /// <summary>
        /// Triggers panic behavior for a single bot.
        /// </summary>
        public static void Trigger(BotComponentCache cache)
        {
            if (cache == null || BotCacheUtility.IsHumanPlayer(cache.Bot))
                return;

            if (TryGetPanicComponent(cache, out var panic))
            {
                panic.TriggerPanic();
            }
        }

        /// <summary>
        /// Triggers panic behavior for all bots in the specified group.
        /// </summary>
        public static void TriggerGroup(List<BotComponentCache> group)
        {
            if (group == null || group.Count == 0)
                return;

            foreach (var cache in group)
            {
                if (cache == null || BotCacheUtility.IsHumanPlayer(cache.Bot))
                    continue;

                if (TryGetPanicComponent(cache, out var panic))
                {
                    panic.TriggerPanic();
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns the bot's visible nickname, or "Unknown" if not available.
        /// </summary>
        private static string GetBotName(BotComponentCache cache)
        {
            return cache?.Bot?.Profile?.Info?.Nickname ?? "Unknown";
        }

        #endregion
    }
}
