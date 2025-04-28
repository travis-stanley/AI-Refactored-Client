#nullable enable

namespace AIRefactored.AI.Reactions
{
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Optimization;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Handles bot reactions to intense light exposure (e.g., flashlights or flashbangs).
    ///     Applies suppression, triggers scored fallback movement, and panic if threshold reached.
    /// </summary>
    public sealed class BotFlashReactionComponent
    {
        private const float FallbackDistance = 5f;

        private const float FallbackJitter = 1.25f;

        private const float MaxSuppressionDuration = 5.0f;

        private const float MinSuppressionDuration = 1.0f;

        private const float ReactionCooldown = 0.5f;

        private BotComponentCache? _cache;

        private float _lastTriggerTime = -1f;

        private float _suppressedUntil = -1f;

        /// <summary>
        ///     Links this flash reaction handler to the active bot's shared component cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache;
        }

        /// <summary>
        ///     Returns true if the bot is still suppressed from a flash reaction.
        /// </summary>
        public bool IsSuppressed()
        {
            return Time.time < this._suppressedUntil;
        }

        /// <summary>
        ///     Call every frame from BotBrain. Updates suppression state.
        /// </summary>
        public void Tick(float time)
        {
            if (time >= this._suppressedUntil) this._suppressedUntil = -1f;
        }

        /// <summary>
        ///     Triggers suppression, fallback, and panic if conditions are met.
        /// </summary>
        public void TriggerSuppression(float strength = 0.6f)
        {
            if (this._cache == null)
                return;

            var bot = this._cache.Bot;
            if (bot == null || bot.IsDead || bot.GetPlayer is not { IsAI: true, IsYourPlayer: false })
                return;

            var now = Time.time;
            if (now - this._lastTriggerTime < ReactionCooldown)
                return;

            this._lastTriggerTime = now;

            var composure = this._cache.PanicHandler?.GetComposureLevel() ?? 1f;
            var scaled = Mathf.Clamp01(strength * composure);
            var duration = Mathf.Lerp(MinSuppressionDuration, MaxSuppressionDuration, scaled);

            this._suppressedUntil = now + duration;

            TriggerFallback(bot);
            TriggerPanic(this._cache);
        }

        private static void TriggerFallback(BotOwner bot)
        {
            // Use scored fallback point if available
            var threatDir = bot.LookDirection;
            var scoredRetreat = HybridFallbackResolver.GetBestRetreatPoint(bot, threatDir);

            if (scoredRetreat.HasValue)
            {
                BotMovementHelper.SmoothMoveTo(bot, scoredRetreat.Value);
                return;
            }

            // Fallback to inertial directional retreat if no cover found
            var forward = bot.LookDirection;
            var lateral = new Vector3(-forward.x, 0f, -forward.z).normalized;
            var fallback = bot.Position + lateral * FallbackDistance + Random.insideUnitSphere * FallbackJitter;
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
    }
}