#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Helpers;

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
        private const float SuppressionDuration = 2.0f;
        private bool _isSuppressed = false;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            if (_bot == null)
            {
                Debug.LogError("[AIRefactored] BotSuppressionReactionComponent missing BotOwner!");
            }
        }

        private void Update()
        {
            if (!_isSuppressed || _bot == null || IsHumanPlayer())
                return;

            if (Time.time - _suppressionStartTime > SuppressionDuration)
                _isSuppressed = false;
        }

        #endregion

        #region Suppression Logic

        /// <summary>
        /// Triggers suppression logic and evasive sprinting away from the source direction.
        /// </summary>
        /// <param name="from">Position of threat (e.g. gunfire or explosion).</param>
        public void TriggerSuppression(Vector3? from = null)
        {
            if (_isSuppressed || _bot == null || IsHumanPlayer())
                return;

            _isSuppressed = true;
            _suppressionStartTime = Time.time;

            Vector3 direction = from.HasValue
                ? (_bot.Position - from.Value).normalized
                : -_bot.LookDirection.normalized;

            Vector3 fallback = _bot.Position + direction * 6f;

            if (Physics.Raycast(_bot.Position, direction, out var hit, 6f))
            {
                fallback = hit.point - direction * 1f;
            }

            _bot.Sprint(true);

            float cohesion = 1.0f;
            if (BotRegistry.Exists(_bot.ProfileId))
            {
                var profile = BotRegistry.Get(_bot.ProfileId);
                cohesion = Mathf.Lerp(0.6f, 1.2f, profile.Cohesion);
            }

            BotMovementHelper.SmoothMoveTo(_bot, fallback, allowSlowEnd: false, cohesionScale: cohesion);
        }

        /// <summary>
        /// External trigger interface for suppression based on incoming direction.
        /// </summary>
        public void ReactToSuppression(Vector3 source)
        {
            TriggerSuppression(source);
        }

        /// <summary>
        /// Returns whether bot is currently suppressed.
        /// </summary>
        public bool IsSuppressed() => _isSuppressed;

        #endregion

        #region Helpers

        private bool IsHumanPlayer()
        {
            return _bot?.GetPlayer != null && !_bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
