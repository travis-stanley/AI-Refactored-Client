#nullable enable

using UnityEngine;
using EFT;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Handles bot reaction to flashlight or flashbang-based suppression events.
    /// </summary>
    public class BotFlashReactionComponent : MonoBehaviour
    {
        public BotOwner? Bot { get; private set; }

        private float _suppressedUntil = -1f;
        private float _lastTriggerTime = -1f;

        private const float MinDuration = 1.0f;
        private const float MaxDuration = 5.0f;
        private const float Cooldown = 0.5f;

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
        }

        private void Update()
        {
            // Passive recovery over time
            if (IsSuppressed() && Time.time >= _suppressedUntil)
            {
#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-Flash] Bot {Bot?.Profile?.Info?.Nickname ?? "?"} recovered from flash suppression.");
#endif
                _suppressedUntil = -1f;
            }
        }

        /// <summary>
        /// Triggers suppression based on flash strength. Stronger = longer panic.
        /// </summary>
        public void TriggerSuppression(float strength = 0.6f)
        {
            if (Bot == null)
                return;

            float timeSinceLast = Time.time - _lastTriggerTime;
            if (timeSinceLast < Cooldown)
                return;

            _lastTriggerTime = Time.time;

            float clampedStrength = Mathf.Clamp01(strength);
            float duration = Mathf.Lerp(MinDuration, MaxDuration, clampedStrength);

            _suppressedUntil = Time.time + duration;

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored-Flash] Bot {Bot.Profile?.Info?.Nickname ?? "?"} suppressed for {duration:F2}s (strength {strength:F2})");
#endif

            TriggerFallbackMovement();
        }

        /// <summary>
        /// Whether bot is actively under flash-based suppression.
        /// </summary>
        public bool IsSuppressed()
        {
            return Time.time < _suppressedUntil;
        }

        /// <summary>
        /// Optional evasive movement on suppression trigger.
        /// </summary>
        private void TriggerFallbackMovement()
        {
            if (Bot == null || Bot.IsDead)
                return;

            Vector3 backward = -Bot.LookDirection.normalized;
            Vector3 retreatPoint = Bot.Position + backward * 5f + Random.insideUnitSphere * 1.5f;
            retreatPoint.y = Bot.Position.y;

            Bot.GoToPoint(retreatPoint, slowAtTheEnd: false);
        }
    }
}
