#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Handles bot reaction to intense light sources (flashbangs, flashlights) and coordinates suppression response.
    /// Integrates with panic, fallback, and tactical behavior logic.
    /// </summary>
    public class BotFlashReactionComponent : MonoBehaviour
    {
        #region Fields

        public BotOwner? Bot { get; private set; }

        private float _suppressedUntil = -1f;
        private float _lastTriggerTime = -1f;

        private const float MinDuration = 1.0f;
        private const float MaxDuration = 5.0f;
        private const float Cooldown = 0.5f;

        private BotOwnerZone? _zone;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
            _zone = GetComponent<BotOwnerZone>();
        }

        private void Update()
        {
            float now = Time.time;
            if (_suppressedUntil > 0f && now >= _suppressedUntil)
            {
                _suppressedUntil = -1f;
            }
        }

        #endregion

        #region Suppression Logic

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

        public bool IsSuppressed() => Time.time < _suppressedUntil;

        #endregion

        #region Fallback + Panic Integration

        private void TriggerFallbackMovement()
        {
            if (Bot == null || Bot.IsDead || Bot.Mover == null || Bot.Transform == null)
                return;

            Vector3 fallbackDir = -Bot.LookDirection;
            Vector3 retreat = Bot.Position + fallbackDir * 5f + Random.insideUnitSphere * 1.5f;
            retreat.y = Bot.Position.y;

            Bot.GoToPoint(retreat, slowAtTheEnd: false);
            _zone?.TriggerFallback(retreat);
        }

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
