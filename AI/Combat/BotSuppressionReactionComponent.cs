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
            {
                _isSuppressed = false;
            }
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

            Vector3 threatDirection = from.HasValue
                ? (_bot.Position - from.Value).normalized
                : -_bot.LookDirection.normalized;

            float cohesion = 1.0f;
            if (BotRegistry.Exists(_bot.ProfileId))
            {
                var profile = BotRegistry.Get(_bot.ProfileId);
                cohesion = Mathf.Clamp(profile.Cohesion, 0.5f, 1.5f);
            }

            Vector3 fallback = _bot.Position + threatDirection * 6f;

            if (_cache?.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, threatDirection, _cache.PathCache);
                if (path.Count > 0)
                    fallback = path[path.Count - 1];
            }

            _bot.Sprint(true);
            BotMovementHelper.SmoothMoveTo(_bot, fallback, false, cohesion);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);

#if UNITY_EDITOR
            Debug.DrawLine(_bot.Position, fallback, Color.red, 1.25f);
#endif
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
