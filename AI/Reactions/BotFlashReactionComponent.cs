#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
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

        public BotOwner? Bot => _cache?.Bot;

        #endregion

        #region Private Fields

        private BotComponentCache? _cache;
        private float _suppressedUntil = -1f;
        private float _lastTriggerTime = -1f;

        private const float MinDuration = 1.0f;
        private const float MaxDuration = 5.0f;
        private const float Cooldown = 0.5f;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
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
            if (_cache?.Bot == null || _cache.Bot.IsDead || !_cache.Bot.GetPlayer?.IsAI == true)
                return;

            if (now - _lastTriggerTime < Cooldown)
                return;

            _lastTriggerTime = now;

            float clampedStrength = Mathf.Clamp01(strength);
            float duration = Mathf.Lerp(MinDuration, MaxDuration, clampedStrength);
            _suppressedUntil = now + duration;

            TriggerFallbackMovement(_cache.Bot.Position - _cache.Bot.LookDirection.normalized);
            TriggerPanicSync();
        }

        public bool IsSuppressed() => Time.time < _suppressedUntil;

        #endregion

        #region Fallback and Panic Integration

        private void TriggerFallbackMovement(Vector3 from)
        {
            if (_cache?.Bot == null || _cache.Bot.IsDead)
                return;

            Vector3 fallbackDir = (_cache.Bot.Position - from).normalized;
            Vector3 retreat = _cache.Bot.Position + fallbackDir * 5f + Random.insideUnitSphere * 1.5f;
            retreat.y = _cache.Bot.Position.y;

            BotMovementHelper.SmoothMoveTo(_cache.Bot, retreat, cohesionScale: 1.0f);
        }

        private void TriggerPanicSync()
        {
            if (_cache?.Bot == null)
                return;

            var player = _cache.Bot.GetPlayer;
            if (player == null || !player.IsAI)
                return;

            if (BotPanicUtility.TryGetPanicComponent(_cache, out var panic))
            {
                panic.TriggerPanic();
            }
        }

        #endregion
    }
}
