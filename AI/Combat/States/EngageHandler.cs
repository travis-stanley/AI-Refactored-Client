#nullable enable

using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat.States
{
    /// <summary>
    /// Handles logic for the Engage state.
    /// Bots move toward last known enemy positions and transition to Attack when within range.
    /// </summary>
    public sealed class EngageHandler
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotOwner _bot;

        #endregion

        #region Constructor

        public EngageHandler(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot!;
        }

        #endregion

        #region Evaluation

        /// <summary>
        /// Determines whether the Engage state should be active.
        /// </summary>
        public bool ShallUseNow()
        {
            return _cache.Combat?.LastKnownEnemyPos.HasValue == true &&
                   !CanAttack(); // Only engage if not yet within attack distance
        }

        /// <summary>
        /// Returns true if the bot is close enough to transition to Attack state.
        /// </summary>
        public bool CanAttack()
        {
            Vector3? pos = _cache.Combat?.LastKnownEnemyPos;
            if (!pos.HasValue)
                return false;

            float dist = Vector3.Distance(_bot.Position, pos.Value);
            float range = _cache.AIRefactoredBotOwner?.PersonalityProfile.EngagementRange ?? 25f;

            return dist < range;
        }

        #endregion

        #region Tick

        /// <summary>
        /// Moves toward the last known enemy position with squad-aware offset.
        /// </summary>
        public void Tick()
        {
            Vector3? lastKnown = _cache.Combat?.LastKnownEnemyPos;
            if (!lastKnown.HasValue)
                return;

            Vector3 destination = _cache.Pathing?.ApplyOffsetTo(lastKnown.Value) ?? lastKnown.Value;

            BotMovementHelper.SmoothMoveTo(_bot, destination);
            _cache.Combat?.TrySetStanceFromNearbyCover(destination);
        }

        #endregion
    }
}
