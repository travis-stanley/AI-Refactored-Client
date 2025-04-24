#nullable enable

using AIRefactored.AI.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Triggers suppression and panic behavior in AIRefactored bots.
    /// Used to simulate enemy fire pressure and flash-based fear effects.
    /// </summary>
    public static class BotSuppressionHelper
    {
        #region Constants

        private const float FlashBlindDuration = 4.5f;

        #endregion

        #region Bot Accessors

        /// <summary>
        /// Gets the BotOwner instance from a Player, if AI-controlled.
        /// </summary>
        public static BotOwner? GetBotOwner(Player? player)
        {
            return player?.IsAI == true && player.AIData is BotOwner owner ? owner : null;
        }

        /// <summary>
        /// Gets the BotComponentCache from a Player, if AI-controlled.
        /// </summary>
        public static BotComponentCache? GetCache(Player? player)
        {
            return player?.IsAI == true ? BotCacheUtility.GetCache(player) : null;
        }

        #endregion

        #region Suppression Logic

        /// <summary>
        /// Triggers suppression effects including panic or fallback visual impairment.
        /// </summary>
        public static void TrySuppressBot(Player? player, Vector3 threatPosition, IPlayer? source = null)
        {
            if (player?.IsAI != true)
                return;

            BotOwner? owner = GetBotOwner(player);
            BotComponentCache? cache = GetCache(player);

            if (owner == null || cache == null || owner.IsDead)
                return;

            // Mark bot as under fire via valid EFT memory API
            owner.Memory?.SetUnderFire(source);

            // Panic if not already panicking, otherwise fallback to blindness
            if (cache.PanicHandler?.IsPanicking != true)
            {
                cache.PanicHandler?.TriggerPanic();
            }
            else
            {
                cache.FlashGrenade?.ForceBlind(FlashBlindDuration);
            }
        }

        /// <summary>
        /// Evaluates if bot is in a situation likely to justify suppression based on visibility and ambient light.
        /// </summary>
        public static bool ShouldTriggerSuppression(Player? player, float visibleDistThreshold = 12f, float ambientThreshold = 0.25f)
        {
            BotOwner? owner = GetBotOwner(player);
            if (owner?.LookSensor == null)
                return false;

            float visibleDist = owner.LookSensor.ClearVisibleDist;
            float ambientLight = RenderSettings.ambientLight.grayscale;

            return visibleDist < visibleDistThreshold || ambientLight < ambientThreshold;
        }

        #endregion
    }
}
