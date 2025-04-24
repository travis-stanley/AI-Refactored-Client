#nullable enable

using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using EFT;
using UnityEngine;
using System;

namespace AIRefactored.AI.Combat.States
{
    /// <summary>
    /// Handles logic for fallback behavior, including suppression retreat and cover repositioning.
    /// Integrates with HybridFallbackResolver and squad-aware cover logic.
    /// </summary>
    public sealed class FallbackHandler
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotOwner _bot;
        private Vector3 _fallbackTarget;

        private const float MinArrivalDistance = 2f;

        #endregion

        #region Constructor

        public FallbackHandler(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot!;
            _fallbackTarget = _bot.Position;
        }

        #endregion

        #region FSM Hooks

        /// <summary>
        /// Whether suppression has persisted long enough to forcibly override current state.
        /// </summary>
        public bool ShouldTriggerSuppressedFallback(float now, float lastStateChangeTime, float minStateDuration)
        {
            return _cache.Suppression?.IsSuppressed() == true &&
                   now - lastStateChangeTime >= minStateDuration;
        }

        /// <summary>
        /// True when fallback should continue being ticked.
        /// </summary>
        public bool ShallUseNow(float time)
        {
            return Vector3.Distance(_bot.Position, _fallbackTarget) > MinArrivalDistance;
        }

        /// <summary>
        /// Called by FSM when fallback state is active. Drives movement and transition.
        /// </summary>
        public void Tick(float time, Vector3? lastKnownEnemyPos, Action<CombatState, float> forceState)
        {
            BotMovementHelper.SmoothMoveTo(_bot, _fallbackTarget);
            BotCoverHelper.TrySetStanceFromNearbyCover(_cache, _fallbackTarget);

            if (Vector3.Distance(_bot.Position, _fallbackTarget) < MinArrivalDistance)
            {
                forceState.Invoke(CombatState.Patrol, time);
                _bot.BotTalk?.TrySay(EPhraseTrigger.NeedHelp);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Updates fallback destination. Typically invoked by CombatStateMachine or fallback planner.
        /// </summary>
        public void SetFallbackTarget(Vector3 target)
        {
            _fallbackTarget = target;
        }

        /// <summary>
        /// Gets the current fallback retreat point.
        /// </summary>
        public Vector3 GetFallbackPosition()
        {
            return _fallbackTarget;
        }

        #endregion
    }
}
