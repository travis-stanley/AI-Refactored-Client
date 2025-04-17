#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Core;
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Simulates bot suppression response — evasive movement and flinch retreat under threat.
    /// Suppression duration dynamically scales with composure level.
    /// </summary>
    public class BotSuppressionReactionComponent : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _suppressionStartTime = -99f;
        private float _currentSuppressionDuration = 2.0f;
        private bool _isSuppressed = false;

        private const float MaxSuppressionDuration = 2.5f;
        private const float MinSuppressionDuration = 0.6f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();

            if (_bot == null)
                Debug.LogError("[AIRefactored] BotSuppressionReactionComponent missing BotOwner!");
        }

        private void Update()
        {
            if (!_isSuppressed || _bot == null || IsHumanPlayer())
                return;

            if (Time.time - _suppressionStartTime > _currentSuppressionDuration)
            {
                _isSuppressed = false;
            }
        }

        #endregion

        #region Suppression Logic

        /// <summary>
        /// Triggers suppression logic and evasive sprinting away from the threat source.
        /// Duration scales with composure level.
        /// </summary>
        /// <param name="from">Optional position of incoming threat (e.g. gunfire).</param>
        public void TriggerSuppression(Vector3? from = null)
        {
            if (_isSuppressed || _bot == null || IsHumanPlayer())
                return;

            _isSuppressed = true;
            _suppressionStartTime = Time.time;

            float composure = 1f;
            if (_cache?.PanicHandler != null)
                composure = _cache.PanicHandler.GetComposureLevel();

            // Less composure = longer suppression duration
            _currentSuppressionDuration = Mathf.Lerp(MaxSuppressionDuration, MinSuppressionDuration, composure);

            Vector3 direction = from.HasValue
                ? (_bot.Position - from.Value).normalized
                : -_bot.LookDirection.normalized;

            Vector3 fallback = _bot.Position + direction * 6f;

            if (Physics.Raycast(_bot.Position, direction, out RaycastHit hit, 6f))
            {
                fallback = hit.point - direction;
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
        /// Public trigger for suppression response.
        /// </summary>
        /// <param name="source">The origin of suppressive fire or explosive.</param>
        public void ReactToSuppression(Vector3 source)
        {
            TriggerSuppression(source);
        }

        /// <summary>
        /// Returns true if the bot is currently reacting to suppression.
        /// </summary>
        public bool IsSuppressed() => _isSuppressed;

        #endregion

        #region Helpers

        /// <summary>
        /// Checks if bot is controlled by a human.
        /// </summary>
        private bool IsHumanPlayer()
        {
            return _bot?.GetPlayer != null && !_bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
