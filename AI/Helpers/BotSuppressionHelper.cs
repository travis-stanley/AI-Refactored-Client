#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Reactions;
using EFT;
using System.Reflection;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility for triggering suppression or panic responses in AI bots.
    /// Wraps reflection-based methods and safely handles fallback behavior.
    /// </summary>
    public static class BotSuppressionHelper
    {
        private static MethodInfo? _setUnderFireMethod;
        private static readonly bool EnableDebugLogs = false;

        #region Bot Accessors

        public static BotOwner? GetBotOwner(Player bot)
        {
            return bot.IsAI && bot.AIData is BotOwner owner ? owner : null;
        }

        public static BotComponentCache? GetCache(Player bot)
        {
            return bot.GetComponent<BotComponentCache>();
        }

        #endregion

        #region Suppression Triggers

        public static void TrySetUnderFire(BotOwner owner)
        {
            if (owner == null || owner.ShootData == null)
                return;

            if (_setUnderFireMethod == null)
            {
                _setUnderFireMethod = owner.ShootData.GetType()
                    .GetMethod("SetUnderFire", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }

            _setUnderFireMethod?.Invoke(owner.ShootData, null);
            LogDebug($"{owner.Profile?.Info?.Nickname ?? "?"} marked under fire.");
        }

        public static void TrySuppressBot(Player bot, Vector3 flashSource)
        {
            if (!bot.IsAI || bot.AIData == null)
                return;

            var cache = GetCache(bot);
            if (cache == null)
                return;

            if (BotPanicUtility.TryGet(cache, out var panic))
            {
                panic.TriggerPanic();
                LogDebug($"{bot.Profile?.Info?.Nickname ?? "?"} triggered PANIC via PanicHandler.");
                return;
            }

            if (cache.TryGetComponent(out BotFlashReactionComponent? flashReaction))
            {
                flashReaction.TriggerSuppression(0.6f); // default suppression intensity
                LogDebug($"{bot.Profile?.Info?.Nickname ?? "?"} triggered SUPPRESSION via FlashReactionComponent.");
            }
        }


        public static bool ShouldTriggerSuppression(Player bot, float visibleDistThreshold = 12f, float ambientThreshold = 0.25f)
        {
            var owner = GetBotOwner(bot);
            if (owner == null || owner.LookSensor == null)
                return false;

            float visibleDist = owner.LookSensor.ClearVisibleDist;
            float ambient = RenderSettings.ambientLight.grayscale;

            return visibleDist < visibleDistThreshold || ambient < ambientThreshold;
        }

        #endregion

        #region Logging

        private static void LogDebug(string msg)
        {
#if UNITY_EDITOR
            if (EnableDebugLogs)
                Debug.Log($"[AIRefactored-Suppress] {msg}");
#endif
        }

        #endregion
    }
}
