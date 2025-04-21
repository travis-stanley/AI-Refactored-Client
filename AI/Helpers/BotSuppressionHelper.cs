#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System;
using System.Reflection;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility for triggering suppression or panic responses in AIRefactored bots.
    /// Supports realistic simulation of enemy fire pressure and flash panic using fallback logic.
    /// </summary>
    public static class BotSuppressionHelper
    {
        #region Reflection

        private static MethodInfo? _setUnderFireMethod;
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Bot Accessors

        /// <summary>
        /// Attempts to retrieve the BotOwner from an AI-controlled Player.
        /// </summary>
        /// <param name="bot">The player to inspect.</param>
        /// <returns>BotOwner if AI and valid; otherwise null.</returns>
        public static BotOwner? GetBotOwner(Player bot)
        {
            return bot.IsAI && bot.AIData is BotOwner owner ? owner : null;
        }

        /// <summary>
        /// Gets the BotComponentCache for a player (if AI-controlled).
        /// </summary>
        /// <param name="bot">The player to query.</param>
        /// <returns>BotComponentCache instance or null.</returns>
        public static BotComponentCache? GetCache(Player bot)
        {
            return BotCacheUtility.GetCache(bot);
        }

        #endregion

        #region Suppression Triggers

        /// <summary>
        /// Attempts to trigger the bot's internal "under fire" flag using reflection.
        /// This simulates enemy fire perception without causing real damage.
        /// </summary>
        /// <param name="owner">The target BotOwner.</param>
        public static void TrySetUnderFire(BotOwner? owner)
        {
            if (owner?.ShootData == null)
                return;

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
                    _log.LogWarning($"[SuppressionHelper] Failed to invoke SetUnderFire: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Applies suppression effects (panic or blind) based on what components are available.
        /// Flash-based suppression fallback will apply if panic is unavailable.
        /// </summary>
        /// <param name="bot">The AI player to affect.</param>
        /// <param name="flashSource">The source position of the threat (for directionally-aware flash).</param>
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
            }
            else if (cache.FlashGrenade != null)
            {
                cache.FlashGrenade.AddBlindEffect(4.5f, flashSource);
            }
        }

        /// <summary>
        /// Determines whether the bot should be suppressed based on visibility range and ambient brightness.
        /// </summary>
        /// <param name="bot">Target AI player.</param>
        /// <param name="visibleDistThreshold">Minimum distance at which bot is considered "trapped."</param>
        /// <param name="ambientThreshold">Minimum ambient brightness considered safe.</param>
        /// <returns>True if suppression logic should be triggered.</returns>
        public static bool ShouldTriggerSuppression(Player bot, float visibleDistThreshold = 12f, float ambientThreshold = 0.25f)
        {
            var owner = GetBotOwner(bot);
            if (owner?.LookSensor == null)
                return false;

            float visibleDist = owner.LookSensor.ClearVisibleDist;
            float ambient = RenderSettings.ambientLight.grayscale;

            return visibleDist < visibleDistThreshold || ambient < ambientThreshold;
        }

        #endregion
    }
}
