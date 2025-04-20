#nullable enable

using AIRefactored.AI.Core;
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
            return BotCacheUtility.GetCache(bot);
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

        }

        public static void TrySuppressBot(Player bot, Vector3 flashSource)
        {
            if (!bot.IsAI || bot.AIData == null)
                return;

            var cache = GetCache(bot);
            if (cache == null)
                return;

            if (cache.PanicHandler != null)
            {
                cache.PanicHandler.TriggerPanic();

                return;
            }

            if (cache.FlashGrenade != null)
            {
                cache.FlashGrenade.AddBlindEffect(4.5f, flashSource);

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

    }
}
