#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Handles bot suppression logic: retreat, sprint, and flinch reactions under heavy fire.
    /// Typically invoked from vision, sound, or damage input.
    /// </summary>
    public class BotSuppressionReactionComponent
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _suppressionStartTime = -99f;
        private bool _isSuppressed = false;

        private const float SuppressionDuration = 2.0f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;
        private static readonly bool DebugEnabled = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes suppression logic with required bot context.
        /// </summary>
        /// <param name="cache">Component cache for the bot.</param>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Tick

        /// <summary>
        /// Updates suppression state for decay/expiration.
        /// Called periodically by BotBrain or logic tick.
        /// </summary>
        /// <param name="now">Current time (Time.time).</param>
        public void Tick(float now)
        {
            if (!_isSuppressed || _bot == null || _bot.IsDead || !_bot.IsAI)
                return;

            if (now - _suppressionStartTime >= SuppressionDuration)
                _isSuppressed = false;
        }

        #endregion

        #region Suppression Triggers

        /// <summary>
        /// Triggers suppression reaction and evasive retreat from threat direction.
        /// </summary>
        /// <param name="from">Optional position to retreat from (e.g., bullet source).</param>
        public void TriggerSuppression(Vector3? from = null)
        {
            if (_isSuppressed || _bot == null || !_bot.IsAI || _bot.IsDead)
                return;

            if (_cache?.PanicHandler?.IsPanicking == true)
                return;

            _isSuppressed = true;
            _suppressionStartTime = Time.time;

            Vector3 threatDir = from.HasValue
                ? (_bot.Position - from.Value).normalized
                : -_bot.LookDirection.normalized;

            float cohesion = 1f;
            if (BotRegistry.Exists(_bot.ProfileId))
            {
                cohesion = Mathf.Clamp(BotRegistry.Get(_bot.ProfileId)?.Cohesion ?? 1f, 0.5f, 1.5f);
            }

            Vector3 fallback = _bot.Position + threatDir * 6f;

            if (_cache?.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, threatDir, _cache.PathCache);
                if (path.Count > 0)
                {
                    fallback = path[path.Count - 1];
                }
            }

            _bot.Sprint(true);
            BotMovementHelper.SmoothMoveTo(_bot, fallback, false, cohesion);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);

            if (DebugEnabled)
                Logger.LogDebug($"[Suppression] {_bot.Profile?.Info?.Nickname ?? "Bot"} falling back to {fallback}");
        }

        /// <summary>
        /// Reacts to suppression from a known position source.
        /// </summary>
        /// <param name="source">World position the bot is being suppressed from.</param>
        public void ReactToSuppression(Vector3 source)
        {
            TriggerSuppression(source);
        }

        /// <summary>
        /// Returns true if the bot is currently suppressed.
        /// </summary>
        public bool IsSuppressed()
        {
            return _isSuppressed;
        }

        #endregion
    }
}
