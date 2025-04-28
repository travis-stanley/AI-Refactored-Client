#nullable enable

namespace AIRefactored.AI.Helpers
{
    using AIRefactored.AI.Core;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Triggers suppression and panic behavior in AIRefactored bots.
    ///     Used to simulate enemy fire pressure and flash-based fear effects.
    /// </summary>
    public static class BotSuppressionHelper
    {
        private const float FlashBlindDuration = 4.5f;

        /// <summary>
        ///     Gets the BotOwner instance from a Player, if AI-controlled.
        /// </summary>
        public static BotOwner? GetBotOwner(Player? player)
        {
            return player?.IsAI == true && player.AIData is BotOwner owner ? owner : null;
        }

        /// <summary>
        ///     Gets the BotComponentCache from a Player, if AI-controlled.
        /// </summary>
        public static BotComponentCache? GetCache(Player? player)
        {
            return player?.IsAI == true ? BotCacheUtility.GetCache(player) : null;
        }

        /// <summary>
        ///     Evaluates whether suppression should occur based on visibility and lighting.
        /// </summary>
        public static bool ShouldTriggerSuppression(
            Player? player,
            float visibleDistThreshold = 12f,
            float ambientThreshold = 0.25f)
        {
            var owner = GetBotOwner(player);
            if (owner?.LookSensor == null)
                return false;

            var visibleDist = owner.LookSensor.ClearVisibleDist;

            // Ambient light may not be reliable on headless or non-rendered environments
            var ambientLight = 0.5f;
            try
            {
                ambientLight = RenderSettings.ambientLight.grayscale;
            }
            catch
            {
            }

            return visibleDist < visibleDistThreshold || ambientLight < ambientThreshold;
        }

        /// <summary>
        ///     Triggers suppression effects for a bot from a given threat source.
        ///     Applies panic or flash-based blindness depending on bot state.
        /// </summary>
        public static void TrySuppressBot(Player? player, Vector3 threatPosition, IPlayer? source = null)
        {
            if (player?.IsAI != true)
                return;

            var owner = GetBotOwner(player);
            var cache = GetCache(player);

            if (owner == null || cache == null || owner.IsDead)
                return;

            // Mark bot as under fire via memory system
            owner.Memory?.SetUnderFire(source);

            // If not already panicking, trigger full panic
            if (cache.PanicHandler?.IsPanicking != true) cache.PanicHandler?.TriggerPanic();
            else

                // If already panicking, apply visual impairment
                cache.FlashGrenade?.ForceBlind();
        }
    }
}