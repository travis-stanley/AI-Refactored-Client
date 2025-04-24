#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Handles bot reactions to intense light exposure (e.g., flashlights or flashbangs).
    /// Applies suppression, triggers scored fallback movement, and panic if threshold reached.
    /// </summary>
    public sealed class BotFlashReactionComponent
    {
        #region Constants

        private const float MinSuppressionDuration = 1.0f;
        private const float MaxSuppressionDuration = 5.0f;
        private const float ReactionCooldown = 0.5f;

        private const float FallbackDistance = 5f;
        private const float FallbackJitter = 1.25f;

        #endregion

        #region Fields

        private BotComponentCache? _cache;
        private float _suppressedUntil = -1f;
        private float _lastTriggerTime = -1f;

        #endregion

        #region Initialization

        /// <summary>
        /// Links this flash reaction handler to the active bot's shared component cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
        }

        #endregion

        #region Tick

        /// <summary>
        /// Call every frame from BotBrain. Updates suppression state.
        /// </summary>
        public void Tick(float time)
        {
            if (time >= _suppressedUntil)
                _suppressedUntil = -1f;
        }

        /// <summary>
        /// Returns true if the bot is still suppressed from a flash reaction.
        /// </summary>
        public bool IsSuppressed() => Time.time < _suppressedUntil;

        #endregion

        #region Reaction Logic

        /// <summary>
        /// Triggers suppression, fallback, and panic if conditions are met.
        /// </summary>
        public void TriggerSuppression(float strength = 0.6f)
        {
            if (_cache == null)
                return;

            var bot = _cache.Bot;
            if (bot == null || bot.IsDead || bot.GetPlayer is not { IsAI: true, IsYourPlayer: false })
                return;

            float now = Time.time;
            if (now - _lastTriggerTime < ReactionCooldown)
                return;

            _lastTriggerTime = now;

            float composure = _cache.PanicHandler?.GetComposureLevel() ?? 1f;
            float scaled = Mathf.Clamp01(strength * composure);
            float duration = Mathf.Lerp(MinSuppressionDuration, MaxSuppressionDuration, scaled);

            _suppressedUntil = now + duration;

            TriggerFallback(bot);
            TriggerPanic(_cache);
        }

        #endregion

        #region Fallback Logic

        private static void TriggerFallback(BotOwner bot)
        {
            // Use scored fallback point if available
            Vector3 threatDir = bot.LookDirection;
            Vector3? scoredRetreat = HybridFallbackResolver.GetBestRetreatPoint(bot, threatDir);

            if (scoredRetreat.HasValue)
            {
                BotMovementHelper.SmoothMoveTo(bot, scoredRetreat.Value);
                return;
            }

            // Fallback to inertial directional retreat if no cover found
            Vector3 forward = bot.LookDirection;
            Vector3 lateral = new Vector3(-forward.x, 0f, -forward.z).normalized;
            Vector3 fallback = bot.Position + lateral * FallbackDistance + Random.insideUnitSphere * FallbackJitter;
            fallback.y = bot.Position.y;

            BotMovementHelper.SmoothMoveTo(bot, fallback);
        }

        private static void TriggerPanic(BotComponentCache? cache)
        {
            if (cache == null)
                return;

            if (BotPanicUtility.TryGetPanicComponent(cache, out var panic) && panic != null)
                panic.TriggerPanic();
        }

        #endregion
    }
}
