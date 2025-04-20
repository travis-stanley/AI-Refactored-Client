#nullable enable

using AIRefactored.AI.Core;
using EFT;
using System;
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

        /// <summary>
        /// Retrieves the BotOwner from a Player if it is AI-controlled.
        /// </summary>
        public static BotOwner? GetBotOwner(Player bot)
        {
            return bot.IsAI && bot.AIData is BotOwner owner ? owner : null;
        }

        /// <summary>
        /// Retrieves the BotComponentCache associated with the Player bot.
        /// </summary>
        public static BotComponentCache? GetCache(Player bot)
        {
            return BotCacheUtility.GetCache(bot);
        }

        #endregion

        #region Suppression Triggers

        /// <summary>
        /// Sets the 'Under Fire' state on the bot if the appropriate method exists.
        /// Uses reflection to invoke the internal method.
        /// </summary>
        public static void TrySetUnderFire(BotOwner owner)
        {
            if (owner == null || owner.ShootData == null)
                return;

            // Cache the reflection method for optimization
            if (_setUnderFireMethod == null)
            {
                _setUnderFireMethod = owner.ShootData.GetType()
                    .GetMethod("SetUnderFire", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            }

            if (_setUnderFireMethod != null)
            {
                try
                {
                    _setUnderFireMethod.Invoke(owner.ShootData, null);
                }
                catch (Exception ex)
                {
                    if (EnableDebugLogs)
                        Debug.LogError($"Failed to invoke 'SetUnderFire' method: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Triggers panic or flash blindness for the specified bot when under suppression.
        /// </summary>
        public static void TrySuppressBot(Player bot, Vector3 flashSource)
        {
            if (!bot.IsAI || bot.AIData == null)
                return;

            var cache = GetCache(bot);
            if (cache == null)
                return;

            if (cache.PanicHandler != null)
            {
                cache.PanicHandler.TriggerPanic();  // Trigger panic if PanicHandler is available
                return;
            }

            if (cache.FlashGrenade != null)
            {
                cache.FlashGrenade.AddBlindEffect(4.5f, flashSource);  // Apply flashbang blindness effect
            }
        }

        /// <summary>
        /// Determines whether suppression should be triggered based on visibility and ambient light.
        /// </summary>
        public static bool ShouldTriggerSuppression(Player bot, float visibleDistThreshold = 12f, float ambientThreshold = 0.25f)
        {
            var owner = GetBotOwner(bot);
            if (owner == null || owner.LookSensor == null)
                return false;

            // Get the visibility distance from the LookSensor and the ambient light level
            float visibleDist = owner.LookSensor.ClearVisibleDist;
            float ambient = RenderSettings.ambientLight.grayscale;

            // Trigger suppression if conditions are met
            return visibleDist < visibleDistThreshold || ambient < ambientThreshold;
        }

        #endregion
    }
}
