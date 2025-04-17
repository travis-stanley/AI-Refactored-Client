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
    /// Utility for triggering suppression or panic responses in AI bots.
    /// Wraps reflection-based methods and safely handles fallback behavior.
    /// </summary>
    public static class BotSuppressionHelper
    {
        #region Reflection Cache

        private static MethodInfo? _setUnderFireMethod;

        #endregion

        #region Bot Accessors

        /// <summary>
        /// Attempts to get the BotOwner from an EFT Player if it's an AI.
        /// </summary>
        public static BotOwner? GetBotOwner(Player bot)
        {
            return bot.IsAI && bot.AIData is BotOwner owner ? owner : null;
        }

        /// <summary>
        /// Gets the BotComponentCache from a Player GameObject.
        /// </summary>
        public static BotComponentCache? GetCache(Player bot)
        {
            return bot.GetComponent<BotComponentCache>();
        }

        #endregion

        #region Suppression Triggers

        /// <summary>
        /// Uses reflection to flag a bot as being under fire (used for suppression logic).
        /// </summary>
        public static void TrySetUnderFire(BotOwner owner)
        {
            if (owner == null || BotCacheUtility.IsHumanPlayer(owner) || owner.ShootData == null)
                return;

            _setUnderFireMethod ??= owner.ShootData.GetType()
                .GetMethod("SetUnderFire", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            _setUnderFireMethod?.Invoke(owner.ShootData, null);
            LogDebug($"{owner.Profile?.Info?.Nickname ?? "?"} marked under fire.");
        }

        /// <summary>
        /// Attempts to suppress a bot from an external source (e.g. gunfire).
        /// </summary>
        public static void TrySuppressBot(Player bot, Vector3 source)
        {
            if (!bot.IsAI || bot.AIData == null || BotCacheUtility.IsHumanPlayer(bot))
                return;

            var cache = GetCache(bot);
            if (cache == null)
                return;

            if (BotPanicUtility.TryGet(cache, out var panic))
            {
                panic.TriggerPanic();
                LogDebug($"{bot.Profile?.Info?.Nickname ?? "?"} triggered PANIC (via BotPanicHandler).");
                return;
            }

            if (cache.TryGetComponent(out BotFlashReactionComponent? reaction))
            {
                reaction.TriggerSuppression();
                LogDebug($"{bot.Profile?.Info?.Nickname ?? "?"} triggered SUPPRESSION (via FlashReactionComponent).");
            }
        }

        /// <summary>
        /// Returns true if a bot is likely to react to suppression due to low vision or ambient light.
        /// </summary>
        public static bool ShouldTriggerSuppression(Player bot, float visibilityThreshold = 12f, float ambientThreshold = 0.25f)
        {
            var owner = GetBotOwner(bot);
            if (owner == null || owner.LookSensor == null)
                return false;

            float visibleDist = owner.LookSensor.ClearVisibleDist;
            float ambient = RenderSettings.ambientLight.grayscale;

            return visibleDist < visibilityThreshold || ambient < ambientThreshold;
        }

        #endregion

        #region Logging

        [System.Diagnostics.Conditional("DEBUG")]
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void LogDebug(string msg) =>
            Debug.Log($"[AIRefactored-Suppress] {msg}");

        #endregion
    }
}
