#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Maintains passive squad cohesion when idle or patrolling:
    /// • Repels bots that are too close
    /// • Follows furthest idle mate if too far
    /// • Adds subtle jitter to mimic organic movement
    /// </summary>
    public sealed class BotGroupBehavior
    {
        #region Constants

        private const float MinSpacing = 2.25f;
        private const float MaxSpacing = 7.5f;
        private const float SpacingTolerance = 0.3f;
        private const float RepulseStrength = 1.25f;
        private const float CohesionWeight = 1.0f;
        private const float JitterAmount = 0.1f;

        private static readonly float MinSpacingSqr = MinSpacing * MinSpacing;
        private static readonly float MaxSpacingSqr = MaxSpacing * MaxSpacing;

        private const bool EnableDebug = false;

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotsGroup? _group;
        private Vector3? _lastMoveTarget;

        /// <summary>Optional group sync logic for fallback and intel sharing.</summary>
        public BotGroupSyncCoordinator? GroupSync { get; private set; }

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
            _group = _bot?.BotsGroup;

            if (_bot != null)
            {
                GroupSync = new BotGroupSyncCoordinator();
                GroupSync.Initialize(_bot);
            }
        }

        #endregion

        #region Tick Loop

        public void Tick(float deltaTime)
        {
            if (!IsEligible())
                return;

            if (_bot!.Memory?.GoalEnemy != null)
                return;

            Vector3 myPos = _bot.Position;
            Vector3 repulsion = Vector3.zero;
            Vector3? furthestTarget = null;
            float maxDistSq = MinSpacingSqr;

            int memberCount = _group?.MembersCount ?? 0;
            for (int i = 0; i < memberCount; i++)
            {
                var mate = _group!.Member(i);
                if (mate == null || mate == _bot || mate.IsDead)
                    continue;

                float distSq = (mate.Position - myPos).sqrMagnitude;

                if (distSq < MinSpacingSqr)
                {
                    Vector3 away = (myPos - mate.Position).normalized;
                    float push = MinSpacing - Mathf.Sqrt(distSq);
                    repulsion += away * push;
                }
                else if (distSq > MaxSpacingSqr && distSq > maxDistSq && mate.Memory?.GoalEnemy == null)
                {
                    maxDistSq = distSq;
                    furthestTarget = mate.Position;
                }
            }

            if (repulsion.sqrMagnitude > 0.01f)
            {
                Vector3 repelTarget = myPos + repulsion.normalized * RepulseStrength;
                IssueMove(repelTarget);
                return;
            }

            if (furthestTarget.HasValue)
            {
                Vector3 dir = (furthestTarget.Value - myPos).normalized;
                Vector3 target = myPos + dir * MaxSpacing;
                IssueMove(target);
            }
        }

        #endregion

        #region Movement Logic

        /// <summary>
        /// Issues a smooth movement command to a jittered target position,
        /// if sufficiently far from the last target. Adds cohesion weighting.
        /// </summary>
        /// <param name="rawTarget">The original movement destination.</param>
        /// <summary>
        /// Issues a smooth movement command to a jittered target position,
        /// if sufficiently far from the last target. Adds cohesion weighting.
        /// </summary>
        /// <param name="rawTarget">The original movement destination.</param>
        private void IssueMove(Vector3 rawTarget)
        {
            if (_bot == null)
                return;

            Vector3 jittered = rawTarget + UnityEngine.Random.insideUnitSphere * JitterAmount;
            jittered.y = rawTarget.y;

            if (!_lastMoveTarget.HasValue || Vector3.Distance(_lastMoveTarget.Value, jittered) > SpacingTolerance)
            {
                BotMovementHelper.SmoothMoveTo(_bot, jittered, false, CohesionWeight);
                _lastMoveTarget = jittered;
            }
        }



        #endregion

        #region Validation

        private bool IsEligible()
        {
            return _bot != null &&
                   !_bot.IsDead &&
                   _bot.GetPlayer != null &&
                   _bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
