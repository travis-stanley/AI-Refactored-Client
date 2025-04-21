#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Core;
using System.Collections.Generic;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Provides panic utility helpers for bots and squads.
    /// Used by vision, flash, suppression, and auditory reaction systems to trigger realistic panic behavior.
    /// </summary>
    public static class BotPanicUtility
    {
        #region Panic Resolution

        /// <summary>
        /// Attempts to resolve a panic handler from the bot's component cache.
        /// </summary>
        /// <param name="cache">The bot's component cache.</param>
        /// <param name="panic">The resolved panic handler (if found).</param>
        /// <returns>True if panic handler is present and valid.</returns>
        public static bool TryGetPanicComponent(BotComponentCache cache, out BotPanicHandler? panic)
        {
            panic = cache?.PanicHandler;
            return panic != null;
        }

        /// <summary>
        /// Backward-compatible alias for TryGetPanicComponent.
        /// </summary>
        /// <param name="cache">The bot's component cache.</param>
        /// <param name="panic">The resolved panic handler (if found).</param>
        /// <returns>True if panic handler is valid.</returns>
        public static bool TryGet(BotComponentCache cache, out BotPanicHandler? panic)
        {
            return TryGetPanicComponent(cache, out panic);
        }

        #endregion

        #region Trigger Methods

        /// <summary>
        /// Triggers panic behavior for a single bot if it is valid, AI-controlled, and not human.
        /// </summary>
        /// <param name="cache">The bot's component cache.</param>
        public static void Trigger(BotComponentCache? cache)
        {
            if (cache == null)
                return;

            var bot = cache.Bot;
            if (bot == null || bot.IsDead || bot.GetPlayer == null || !bot.GetPlayer.IsAI)
                return;

            if (TryGetPanicComponent(cache, out var panic) && panic != null)
            {
                panic.TriggerPanic();
            }
        }

        /// <summary>
        /// Triggers panic behavior for all members of the given bot group.
        /// Only applies to living AI bots.
        /// </summary>
        /// <param name="group">List of bot component caches in the same group.</param>
        public static void TriggerGroup(List<BotComponentCache>? group)
        {
            if (group == null || group.Count == 0)
                return;

            for (int i = 0; i < group.Count; i++)
            {
                var cache = group[i];
                if (cache == null)
                    continue;

                var bot = cache.Bot;
                if (bot == null || bot.IsDead || bot.GetPlayer == null || !bot.GetPlayer.IsAI)
                    continue;

                if (TryGetPanicComponent(cache, out var panic) && panic != null)
                {
                    panic.TriggerPanic();
                }
            }
        }

        #endregion
    }
}
