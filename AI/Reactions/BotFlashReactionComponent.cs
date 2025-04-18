#nullable enable

using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Handles bot reaction to intense light sources (e.g., flashbangs, flashlights) by triggering suppression,
    /// fallback movement, and panic synchronization. Enhances realism under light-based threats.
    /// </summary>
    public class BotFlashReactionComponent : MonoBehaviour
    {
        #region Public Properties

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

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
        }

        #endregion

        #region External Tick

        public void Tick(float time)
        {
            if (_suppressedUntil > 0f && time >= _suppressedUntil)
                _suppressedUntil = -1f;
        }

        #endregion

        #region Suppression Logic

        public void TriggerSuppression(float strength = 0.6f)
        {
            float now = Time.time;

            if (Bot == null || Bot.IsDead || !Bot.GetPlayer?.IsAI == true)
                return;

            if (now - _lastTriggerTime < Cooldown)
                return;

            _lastTriggerTime = now;

            float clampedStrength = Mathf.Clamp01(strength);
            float duration = Mathf.Lerp(MinDuration, MaxDuration, clampedStrength);
            _suppressedUntil = now + duration;

            TriggerFallbackMovement(Bot.Position - Bot.LookDirection.normalized);
            TriggerPanicSync();
        }

        public bool IsSuppressed() => Time.time < _suppressedUntil;

        #endregion

        #region Fallback and Panic Integration

        private void TriggerFallbackMovement(Vector3 from)
        {
            if (Bot == null || Bot.IsDead || Bot.Transform == null)
                return;

            Vector3 fallbackDir = (Bot.Position - from).normalized;
            Vector3 retreat = Bot.Position + fallbackDir * 5f + Random.insideUnitSphere * 1.5f;
            retreat.y = Bot.Position.y;

            BotMovementHelper.SmoothMoveTo(Bot, retreat, cohesionScale: 1.0f);
        }

        private void TriggerPanicSync()
        {
            if (Bot == null)
                return;

            var player = Bot.GetPlayer;
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
