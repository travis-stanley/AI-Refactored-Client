#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Core;
using System.Collections.Generic;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility class for resolving and triggering panic behavior in bots and squads.
    /// Used by flash, suppression, auditory, and damage systems to propagate fear.
    /// </summary>
    public static class BotPanicUtility
    {
        #region Panic Resolution

        /// <summary>
        /// Attempts to retrieve the panic handler from a bot’s component cache.
        /// </summary>
        /// <param name="cache">Bot's component cache.</param>
        /// <param name="panic">Output panic handler, if found.</param>
        public static bool TryGetPanicComponent(BotComponentCache? cache, out BotPanicHandler? panic)
        {
            panic = cache?.PanicHandler;
            return panic != null;
        }

        /// <summary>
        /// Alias for TryGetPanicComponent. Preserved for legacy code.
        /// </summary>
        public static bool TryGet(BotComponentCache? cache, out BotPanicHandler? panic)
        {
            return TryGetPanicComponent(cache, out panic);
        }

        #endregion

        #region Panic Triggers

        /// <summary>
        /// Triggers panic on a single bot if valid and not dead.
        /// </summary>
        public static void Trigger(BotComponentCache? cache)
        {
            if (cache == null || cache.Bot == null)
                return;

            var bot = cache.Bot;
            if (bot.IsDead || bot.GetPlayer == null || !bot.GetPlayer.IsAI)
                return;

            cache.PanicHandler?.TriggerPanic();
        }

        /// <summary>
        /// Triggers panic across a squad of bots.
        /// </summary>
        public static void TriggerGroup(List<BotComponentCache>? group)
        {
            if (group == null)
                return;

            for (int i = 0; i < group.Count; i++)
            {
                BotComponentCache? cache = group[i];
                if (cache == null || cache.Bot == null)
                    continue;

                var bot = cache.Bot;
                if (bot.IsDead || bot.GetPlayer == null || !bot.GetPlayer.IsAI)
                    continue;

                cache.PanicHandler?.TriggerPanic();
            }
        }

        #endregion
    }
}
