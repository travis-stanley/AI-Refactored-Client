#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Simulates bot suppression response — evasive movement and flinch retreat under threat.
    /// Intended to be triggered externally by audio cues or visible fire.
    /// </summary>
    public class BotSuppressionReactionComponent
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _suppressionStartTime = -99f;
        private bool _isSuppressed = false;

        private const float SuppressionDuration = 2.0f;
        private const float RetreatDistance = 6.0f;

        #endregion

        #region Init

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Tick Logic

        /// <summary>
        /// Invoked externally by BotBrain for suppression decay.
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

            // Optionally use retreat planner for more realistic cover fallback
            if (_cache?.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, direction, _cache.PathCache);
                if (path.Count > 0)
                    fallback = path[path.Count - 1];
            }

            _bot.Sprint(true);
            BotMovementHelper.SmoothMoveTo(_bot, fallback, false, cohesion);

            _bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);

            Debug.DrawLine(_bot.Position, fallback, Color.red, 1.25f);
        }

        /// <summary>
        /// Triggers suppression from a known source location.
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
