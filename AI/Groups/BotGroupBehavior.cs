#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Handles group-relative behavior: spacing, stack-up movement, and dynamic follow logic.
    /// This runs during non-combat situations to maintain loose squad cohesion.
    /// </summary>
    public class BotGroupBehavior
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotsGroup? _group;

        private const float SpacingMin = 2.25f;
        private const float SpacingMax = 7.5f;
        private const float MoveCohesion = 1.0f;
        private const float SpacingTolerance = 0.3f;
        private const float RepulseStrength = 1.25f;

        private Vector3? _lastFollowTarget;
        private float _lastAdjustTime = -1f;

        #endregion

        #region Public Properties

        /// <summary>
        /// Shared squad-level sync coordinator for fallback, loot, and danger coordination.
        /// </summary>
        public BotGroupSyncCoordinator? GroupSync { get; private set; }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the bot's group behavior module and sync coordinator.
        /// </summary>
        /// <param name="cache">AIRefactored bot component cache.</param>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
            _group = _bot?.BotsGroup;

            GroupSync = new BotGroupSyncCoordinator();
            GroupSync.Initialize(_bot!);
        }

        #endregion

        #region Update Loop

        /// <summary>
        /// Called every frame to adjust squad spacing and following behavior during patrol or idle phases.
        /// </summary>
        /// <param name="deltaTime">Delta time since last frame.</param>
        public void Tick(float deltaTime)
        {
            if (_bot == null || _bot.IsDead || _group == null)
                return;

            // Skip if currently engaged in combat
            if (_bot.Memory?.GoalEnemy != null)
                return;

            Vector3 myPos = _bot.Position;
            Vector3 repulsion = Vector3.zero;
            Vector3? followTarget = null;
            float minDist = float.MaxValue;

            for (int i = 0; i < _group.MembersCount; i++)
            {
                BotOwner mate = _group.Member(i);
                if (mate == null || mate == _bot || mate.IsDead)
                    continue;

                float dist = Vector3.Distance(myPos, mate.Position);

                // Repulsion: keep distance if too close
                if (dist < SpacingMin)
                {
                    repulsion += (myPos - mate.Position).normalized * (SpacingMin - dist);
                }

                // Follow: pick furthest squadmate within follow range who isn't in combat
                if (dist > SpacingMax && dist < minDist && mate.Memory?.GoalEnemy == null)
                {
                    followTarget = mate.Position;
                    minDist = dist;
                }
            }

            // Apply repulsion logic
            if (repulsion.sqrMagnitude > 0.01f)
            {
                Vector3 repelTarget = myPos + repulsion.normalized * RepulseStrength;
                BotMovementHelper.SmoothMoveTo(_bot, repelTarget, false, MoveCohesion);
                _lastFollowTarget = null;
                _lastAdjustTime = Time.time;
                return;
            }

            // Apply follow logic
            if (followTarget.HasValue)
            {
                if (!_lastFollowTarget.HasValue || Vector3.Distance(_lastFollowTarget.Value, followTarget.Value) > SpacingTolerance)
                {
                    Vector3 dir = (followTarget.Value - myPos).normalized;
                    Vector3 followPoint = myPos + dir * SpacingMax;

                    BotMovementHelper.SmoothMoveTo(_bot, followPoint, false, MoveCohesion);
                    _lastFollowTarget = followTarget;
                    _lastAdjustTime = Time.time;
                }
            }
        }

        #endregion
    }
}
