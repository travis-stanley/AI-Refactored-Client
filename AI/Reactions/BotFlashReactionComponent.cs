#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Handles bot reaction to intense light sources (e.g., flashbangs, flashlights) by triggering suppression,
    /// fallback movement, and panic synchronization. Enhances realism under light-based threats.
    /// </summary>
    public class BotFlashReactionComponent
    {
        #region Public Properties

        /// <summary>
        /// Returns the bot instance associated with this component.
        /// </summary>
        public BotOwner? Bot => _cache?.Bot;

        #endregion

        #region Private Fields

        private BotComponentCache? _cache;
        private float _suppressedUntil = -1f;
        private float _lastTriggerTime = -1f;

        private const float MinDuration = 1.0f;
        private const float MaxDuration = 5.0f;
        private const float Cooldown = 0.5f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;
        private static readonly bool DebugEnabled = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the flash reaction component with the bot's component cache.
        /// </summary>
        /// <param name="cache">Bot component cache.</param>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
        }

        #endregion

        #region External Tick

        /// <summary>
        /// Called each frame to update suppression status and clear expired states.
        /// </summary>
        /// <param name="time">Current world time.</param>
        public void Tick(float time)
        {
            if (_suppressedUntil > 0f && time >= _suppressedUntil)
            {
                _suppressedUntil = -1f;

                if (DebugEnabled)
                    Logger.LogDebug($"[BotFlashReaction] Suppression ended for {_cache?.Bot?.Profile?.Id}");
            }
        }

        #endregion

        #region Suppression Logic

        /// <summary>
        /// Triggers suppression behavior from light-based threats.
        /// </summary>
        /// <param name="strength">The intensity of the light or flash.</param>
        public void TriggerSuppression(float strength = 0.6f)
        {
            if (_cache?.Bot is not BotOwner bot || bot.IsDead || bot.GetPlayer?.IsAI != true)
                return;

            float now = Time.time;
            if (now - _lastTriggerTime < Cooldown)
                return;

            _lastTriggerTime = now;

            float composure = _cache.PanicHandler?.GetComposureLevel() ?? 1f;
            float finalStrength = Mathf.Clamp01(strength * (1f - (1f - composure)));
            float duration = Mathf.Lerp(MinDuration, MaxDuration, finalStrength);

            _suppressedUntil = now + duration;

            if (DebugEnabled)
                Logger.LogDebug($"[BotFlashReaction] Suppression triggered for {bot.Profile?.Id} → Duration: {duration:F2}s");

            Vector3 fallbackDir = bot.Position - bot.LookDirection.normalized;
            TriggerFallbackMovement(fallbackDir);
            TriggerPanicSync();
        }

        /// <summary>
        /// Returns true if the bot is currently suppressed.
        /// </summary>
        public bool IsSuppressed() => Time.time < _suppressedUntil;

        #endregion

        #region Fallback and Panic Integration

        /// <summary>
        /// Moves the bot away from the light source in a randomized but realistic retreat path.
        /// </summary>
        /// <param name="from">The direction of threat to move away from.</param>
        private void TriggerFallbackMovement(Vector3 from)
        {
            if (_cache?.Bot is not BotOwner bot || bot.IsDead)
                return;

            Vector3 fallbackDir = (bot.Position - from).normalized;
            Vector3 retreat = bot.Position + fallbackDir * 5f + Random.insideUnitSphere * 1.25f;
            retreat.y = bot.Position.y;

            if (DebugEnabled)
                Logger.LogDebug($"[BotFlashReaction] Retreat path calculated to {retreat}");

            BotMovementHelper.SmoothMoveTo(bot, retreat, cohesionScale: 1f);
        }

        /// <summary>
        /// Propagates panic event to bot’s internal panic component.
        /// </summary>
        private void TriggerPanicSync()
        {
            if (_cache == null)
                return;

            BotOwner? bot = _cache.Bot;
            if (bot == null || bot.GetPlayer?.IsAI != true)
                return;

            if (BotPanicUtility.TryGetPanicComponent(_cache, out var panic) && panic != null)
            {
                panic.TriggerPanic();

                if (DebugEnabled)
                {
                    string id = bot.Profile?.Id ?? "unknown";
                    Logger.LogDebug($"[BotFlashReaction] Panic triggered for {id}");
                }
            }
        }

        #endregion
    }
}
