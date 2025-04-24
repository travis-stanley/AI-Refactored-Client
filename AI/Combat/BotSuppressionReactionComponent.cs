#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using EFT;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Handles bot suppression logic, including flinch, sprint retreat, and composure effects.
    /// Suppression is triggered by incoming fire, sound cues, or visual threats.
    /// </summary>
    public sealed class BotSuppressionReactionComponent
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _suppressionStartTime = -99f;
        private bool _isSuppressed;

        private const float SuppressionDuration = 2.0f;
        private const float MinSuppressionRetreatDistance = 6f;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the suppression logic with required bot cache and context.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _bot = cache.Bot ?? throw new ArgumentNullException(nameof(cache.Bot));
        }

        #endregion

        #region Tick

        /// <summary>
        /// Updates suppression state and clears expired flags.
        /// </summary>
        public void Tick(float now)
        {
            if (!_isSuppressed || !IsValid())
                return;

            if (now - _suppressionStartTime >= SuppressionDuration)
                _isSuppressed = false;
        }

        #endregion

        #region Suppression Logic

        /// <summary>
        /// Triggers bot suppression behavior, causing panic-based fallback with cohesion.
        /// Skips suppression if bot is already suppressed or panicking.
        /// </summary>
        /// <param name="from">Optional position of incoming fire or danger.</param>
        public void TriggerSuppression(Vector3? from = null)
        {
            if (!IsValid() || _isSuppressed || _cache?.PanicHandler?.IsPanicking == true)
                return;

            _isSuppressed = true;
            _suppressionStartTime = Time.time;

            Vector3 retreatDir = from.HasValue
                ? (_bot!.Position - from.Value).normalized
                : -_bot!.LookDirection.normalized;

            Vector3 fallback = CalculateFallback(retreatDir);
            float cohesion = BotRegistry.Get(_bot.ProfileId)?.Cohesion ?? 1f;

            _bot.Sprint(true);
            BotMovementHelper.SmoothMoveTo(_bot, fallback, false, cohesion);

            _cache?.PanicHandler?.TriggerPanic();
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);
        }

        /// <summary>
        /// Direct reaction wrapper for suppression from a known source.
        /// </summary>
        public void ReactToSuppression(Vector3 source)
        {
            TriggerSuppression(source);
        }

        /// <summary>
        /// Returns true if the bot is actively suppressed.
        /// </summary>
        public bool IsSuppressed()
        {
            return _isSuppressed;
        }

        #endregion

        #region Helpers

        private Vector3 CalculateFallback(Vector3 retreatDir)
        {
            Vector3 fallback = _bot!.Position + retreatDir * MinSuppressionRetreatDistance;

            if (_cache?.PathCache != null)
            {
                List<Vector3> path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, retreatDir, _cache.PathCache);
                if (path.Count > 0)
                {
                    fallback = Vector3.Distance(path[0], _bot.Position) < 1f && path.Count > 1
                        ? path[1]
                        : path[0];
                }
            }

            return fallback;
        }

        private bool IsValid()
        {
            return _bot != null &&
                   _cache != null &&
                   !_bot.IsDead &&
                   _bot.GetPlayer != null &&
                   _bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
