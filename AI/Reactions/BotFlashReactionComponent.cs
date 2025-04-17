#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Handles bot reaction to intense light sources (e.g., flashbangs, flashlights) by triggering suppression,
    /// fallback movement, and panic synchronization. Enhances realism under light-based threats.
    /// </summary>
    public class BotFlashReactionComponent : MonoBehaviour
    {
        #region Public Properties

        /// <summary>
        /// Reference to the bot owner attached to this component.
        /// </summary>
        public BotOwner? Bot { get; private set; }

        #endregion

        #region Private Fields

        private float _suppressedUntil = -1f;
        private float _lastTriggerTime = -1f;

        private const float MinDuration = 1.0f;
        private const float MaxDuration = 5.0f;
        private const float Cooldown = 0.5f;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Initializes the bot reference during Awake.
        /// </summary>
        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
        }

        /// <summary>
        /// Resets suppression status after the suppression period expires.
        /// </summary>
        private void Update()
        {
            float now = Time.time;

            if (_suppressedUntil > 0f && now >= _suppressedUntil)
                _suppressedUntil = -1f;
        }

        #endregion

        #region Suppression Logic

        /// <summary>
        /// Triggers suppression on the bot with a strength value between 0 and 1.
        /// Initiates fallback movement and panic behavior.
        /// </summary>
        /// <param name="strength">Intensity of the flash effect (0–1). Higher values last longer.</param>
        public void TriggerSuppression(float strength = 0.6f)
        {
            float now = Time.time;

            if (Bot == null || !Bot.GetPlayer?.IsAI == true || Bot.IsDead)
                return;

            if (now - _lastTriggerTime < Cooldown)
                return;

            _lastTriggerTime = now;

            float clampedStrength = Mathf.Clamp01(strength);
            float duration = Mathf.Lerp(MinDuration, MaxDuration, clampedStrength);
            _suppressedUntil = now + duration;

            TriggerFallbackMovement();
            TriggerPanicSync();
        }

        /// <summary>
        /// Returns whether the bot is currently suppressed.
        /// </summary>
        public bool IsSuppressed() => Time.time < _suppressedUntil;

        #endregion

        #region Fallback and Panic Integration

        /// <summary>
        /// Calculates a retreat vector and commands the bot to move away from the flash direction.
        /// </summary>
        private void TriggerFallbackMovement()
        {
            if (Bot == null || Bot.IsDead || Bot.Mover == null || Bot.Transform == null)
                return;

            Vector3 fallbackDir = -Bot.LookDirection;
            Vector3 retreat = Bot.Position + fallbackDir * 5f + Random.insideUnitSphere * 1.5f;
            retreat.y = Bot.Position.y;

            Bot.GoToPoint(retreat, slowAtTheEnd: false);
        }

        /// <summary>
        /// Synchronizes panic behavior via the panic utility and shared cache.
        /// </summary>
        private void TriggerPanicSync()
        {
            var player = Bot?.GetPlayer;
            if (player == null || !player.IsAI)
                return;

            var cache = BotCacheUtility.GetCache(player);
            if (cache != null && BotPanicUtility.TryGetPanicComponent(cache, out var panic))
            {
                panic.TriggerPanic();
            }
        }

        #endregion
    }
}
