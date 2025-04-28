#nullable enable

namespace AIRefactored.AI.Helpers
{
    using System.Collections.Generic;

    using AIRefactored.AI.Combat;
    using AIRefactored.AI.Core;

    /// <summary>
    ///     Utility class for resolving and triggering panic behavior in bots and squads.
    ///     Used by flash, suppression, auditory, and damage systems to propagate fear.
    /// </summary>
    public static class BotPanicUtility
    {
        /// <summary>
        ///     Triggers panic on a single bot if valid and eligible.
        /// </summary>
        public static void Trigger(BotComponentCache? cache)
        {
            if (cache?.Bot?.IsDead == false && cache.Bot.GetPlayer?.IsAI == true) cache.PanicHandler?.TriggerPanic();
        }

        /// <summary>
        ///     Triggers panic across an entire squad or cache group.
        /// </summary>
        public static void TriggerGroup(List<BotComponentCache>? group)
        {
            if (group == null)
                return;

            for (var i = 0; i < group.Count; i++)
            {
                var cache = group[i];
                if (cache?.Bot?.IsDead == false && cache.Bot.GetPlayer?.IsAI == true)
                    cache.PanicHandler?.TriggerPanic();
            }
        }

        /// <summary>
        ///     Legacy alias for TryGetPanicComponent. Kept for compatibility with older subsystems.
        /// </summary>
        public static bool TryGet(BotComponentCache? cache, out BotPanicHandler? panic)
        {
            return TryGetPanicComponent(cache, out panic);
        }

        /// <summary>
        ///     Attempts to retrieve the panic handler from a bot’s component cache.
        /// </summary>
        public static bool TryGetPanicComponent(BotComponentCache? cache, out BotPanicHandler? panic)
        {
            panic = cache?.PanicHandler;
            return panic != null;
        }

        // TODO: Add TriggerNearby(Vector3 origin, float radius) 
        // to panic bots in close proximity (used for fear propagation).
    }
}