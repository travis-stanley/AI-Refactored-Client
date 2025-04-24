#nullable enable

using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat.States
{
    /// <summary>
    /// Controls behavior during direct combat engagements.
    /// Drives toward target, aligns stance, and updates destination dynamically.
    /// </summary>
    public sealed class AttackHandler
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotOwner _bot;
        private Vector3? _lastTarget;

        #endregion

        #region Constructor

        public AttackHandler(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot!;
        }

        #endregion

        #region Evaluation

        /// <summary>
        /// Returns true if a valid enemy is currently targeted.
        /// </summary>
        public bool ShallUseNow()
        {
            var enemy = _cache.ThreatSelector?.CurrentTarget ?? _bot.Memory?.GoalEnemy?.Person;
            return enemy != null && enemy.HealthController?.IsAlive == true;
        }

        #endregion

        #region Combat Tick

        /// <summary>
        /// Drives movement to enemy target. Only updates path if target has moved significantly.
        /// </summary>
        public void Tick(float time)
        {
            var enemy = _cache.ThreatSelector?.CurrentTarget ?? _bot.Memory?.GoalEnemy?.Person;
            if (enemy == null || enemy.HealthController?.IsAlive != true)
                return;

            Vector3 targetPos = enemy.Transform.position;

            if (!_lastTarget.HasValue || Vector3.Distance(_lastTarget.Value, targetPos) > 1.0f)
            {
                _lastTarget = targetPos;

                Vector3 destination = _cache.SquadPath?.ApplyOffsetTo(targetPos) ?? targetPos;
                BotMovementHelper.SmoothMoveTo(_bot, destination);
                BotCoverHelper.TrySetStanceFromNearbyCover(_cache, destination);
            }
        }

        /// <summary>
        /// Clears the current attack target. Called when combat conditions reset.
        /// </summary>
        public void ClearTarget()
        {
            _lastTarget = null;
        }

        #endregion
    }
}
