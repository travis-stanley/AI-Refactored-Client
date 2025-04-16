#nullable enable

using System;
using System.Reflection;
using EFT;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Reactions;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility for triggering suppression and panic behavior in bots.
    /// </summary>
    public static class BotSuppressionHelper
    {
        private static MethodInfo? _setUnderFireMethod;

        #region Bot Access

        public static BotOwner? GetBotOwner(Player bot)
        {
            return bot.IsAI && bot.AIData is BotOwner owner ? owner : null;
        }

        public static BotComponentCache? GetCache(Player bot)
        {
            return bot.GetComponent<BotComponentCache>();
        }

        #endregion

        #region Suppression Logic

        public static void TrySetUnderFire(BotOwner owner)
        {
            if (owner?.ShootData == null)
                return;

            _setUnderFireMethod ??= owner.ShootData.GetType()
                .GetMethod("SetUnderFire", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            _setUnderFireMethod?.Invoke(owner.ShootData, null);
        }

        public static void TrySuppressBot(Player bot, Vector3 source)
        {
            if (!bot.IsAI || bot.AIData == null)
                return;

            var cache = GetCache(bot);
            if (cache == null)
                return;

            if (BotPanicUtility.TryGet(cache, out var panic))
            {
                panic.TriggerPanic();
            }
            else if (cache.TryGetComponent(out BotFlashReactionComponent? reaction))
            {
                reaction.TriggerSuppression();
            }

#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[AIRefactored-Suppress] {bot.Profile?.Info?.Nickname ?? "?"} triggered suppression.");
#endif
        }

        public static bool ShouldTriggerSuppression(Player bot, float visibilityThreshold = 12f, float ambientThreshold = 0.25f)
        {
            var owner = GetBotOwner(bot);
            if (owner?.LookSensor == null)
                return false;

            float visibleDist = owner.LookSensor.ClearVisibleDist;
            float ambient = RenderSettings.ambientLight.grayscale;

            return visibleDist < visibilityThreshold || ambient < ambientThreshold;
        }

        #endregion

        #region Debug

        [System.Diagnostics.Conditional("DEBUG")]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogDebug(string msg) =>
            UnityEngine.Debug.Log($"[AIRefactored-Suppress] {msg}");

        #endregion
    }
}
