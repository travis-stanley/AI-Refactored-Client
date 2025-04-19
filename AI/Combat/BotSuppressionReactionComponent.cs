#nullable enable

using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Simulates bot suppression response — evasive movement and flinch retreat under threat.
    /// Intended to be triggered externally by audio cues or visible fire.
    /// </summary>
    public class BotSuppressionReactionComponent : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private float _suppressionStartTime = -99f;
        private bool _isSuppressed = false;

        private const float SuppressionDuration = 2.0f;
        private const float RetreatDistance = 6.0f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            if (_bot == null)
                Debug.LogError("[AIRefactored] BotSuppressionReactionComponent missing BotOwner!");
        }

        private void Update()
        {
            // Legacy fallback in case not driven by BotBrain
            Tick(Time.time);
        }

        #endregion

        #region Tick Logic

        /// <summary>
        /// Async-compatible tick invoked externally (BotBrain preferred).
        /// </summary>
        public void Tick(float now)
        {
            if (!_isSuppressed || _bot == null || !_bot.IsAI || _bot.IsDead)
                return;

            if (now - _suppressionStartTime > SuppressionDuration)
                _isSuppressed = false;
        }

        #endregion

        #region Suppression Triggers

        /// <summary>
        /// Triggers suppression logic and evasive fallback sprinting.
        /// </summary>
        public void TriggerSuppression(Vector3? from = null)
        {
            if (_isSuppressed || _bot == null || !_bot.IsAI || _bot.IsDead)
                return;

            _isSuppressed = true;
            _suppressionStartTime = Time.time;

            Vector3 direction = from.HasValue
                ? (_bot.Position - from.Value).normalized
                : -_bot.LookDirection.normalized;

            Vector3 fallback = _bot.Position + direction * RetreatDistance;
            fallback += Random.insideUnitSphere * 0.75f;
            fallback.y = _bot.Position.y;

            float cohesion = 1.0f;
            if (BotRegistry.Exists(_bot.ProfileId))
            {
                var profile = BotRegistry.Get(_bot.ProfileId);
                cohesion = Mathf.Lerp(0.6f, 1.2f, profile.Cohesion);
            }

            _bot.Sprint(true);
            BotMovementHelper.SmoothMoveTo(_bot, fallback, false, cohesion);
        }

        /// <summary>
        /// Triggers suppression from a specific threat origin.
        /// </summary>
        public void ReactToSuppression(Vector3 source)
        {
            TriggerSuppression(source);
        }

        /// <summary>
        /// Returns true if bot is currently suppressed.
        /// </summary>
        public bool IsSuppressed() => _isSuppressed;

        #endregion
    }
}
