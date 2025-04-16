#nullable enable

using System.Collections.Generic;
using UnityEngine;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Core;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Triggers panic states in bots, either individually or in squads.
    /// Used by flashlight, suppression, and perception logic.
    /// </summary>
    public static class BotPanicUtility
    {
        /// <summary>
        /// Attempts to fetch the BotPanicHandler component from cache.
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
        /// Safe alias for TryGetPanicComponent to support legacy helpers.
        /// </summary>
        public static bool TryGet(BotComponentCache cache, out BotPanicHandler? panic)
        {
            return TryGetPanicComponent(cache, out panic);
        }

        /// <summary>
        /// Triggers panic on a single bot from cache.
        /// </summary>
        public static void Trigger(BotComponentCache cache)
        {
            if (cache == null)
                return;

            if (TryGetPanicComponent(cache, out var panic))
            {
                panic.TriggerPanic();

#if UNITY_EDITOR
                string name = cache.Bot?.Profile?.Info?.Nickname ?? "Unknown";
                Debug.Log($"[AIRefactored-Panic] Bot {name} triggered individual panic.");
#endif
            }
        }

        /// <summary>
        /// Triggers panic on a group of bot caches (e.g., squadmates).
        /// </summary>
        public static void TriggerGroup(List<BotComponentCache> group)
        {
            if (group == null || group.Count == 0)
                return;

            for (int i = 0; i < group.Count; i++)
            {
                var cache = group[i];
                if (cache == null)
                    continue;

                if (TryGetPanicComponent(cache, out var panic))
                {
                    panic.TriggerPanic();

#if UNITY_EDITOR
                    string name = cache.Bot?.Profile?.Info?.Nickname ?? "Unknown";
                    Debug.Log($"[AIRefactored-Panic] Group member {name} entered panic.");
#endif
                }
            }
        }
    }
}
