#nullable enable

using AIRefactored.AI.Core;
using EFT;
using System;
using UnityEngine;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Modifies a bot’s movement vector by applying chaos wobble, squad staggering offsets,
    /// and teammate collision avoidance. Produces natural movement flow and reduces clustering.
    /// </summary>
    public class BotMovementTrajectoryPlanner
    {
        #region Constants

        private const float BaseChaosInterval = 0.4f;
        private const float BaseChaosRadius = 0.65f;
        private const float AvoidanceRadius = 2.0f;
        private const float SquadOffsetStrength = 0.75f;
        private const float AvoidanceStrength = 1.25f;

        #endregion

        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;

        private Vector3 _chaosOffset = Vector3.zero;
        private float _nextChaosUpdate;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a movement planner tied to the bot’s personality and squad behavior.
        /// </summary>
        public BotMovementTrajectoryPlanner(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        #endregion

        #region Public API

        /// <summary>
        /// Modifies a target movement vector by applying chaotic offset, squad offset,
        /// and dynamic teammate avoidance.
        /// </summary>
        public Vector3 ModifyTrajectory(Vector3 targetDir, float deltaTime)
        {
            float now = Time.time;

            // === Chaos wobble from personality caution ===
            if (now >= _nextChaosUpdate)
            {
                float caution = _cache.AIRefactoredBotOwner?.PersonalityProfile?.Caution ?? 0.5f;
                float chaosRange = BaseChaosRadius * (1f - caution);

                _chaosOffset = new Vector3(
                    UnityEngine.Random.Range(-chaosRange, chaosRange),
                    0f,
                    UnityEngine.Random.Range(-chaosRange, chaosRange)
                );

                _nextChaosUpdate = now + BaseChaosInterval;
            }

            Vector3 baseDir = targetDir.normalized;

            // === Squad staggering ===
            Vector3 squadOffset = _cache.SquadPath?.GetCurrentOffset() ?? Vector3.zero;
            if (squadOffset.sqrMagnitude > 0.01f)
                squadOffset = squadOffset.normalized * SquadOffsetStrength;

            // === Teammate proximity avoidance ===
            Vector3 avoidance = Vector3.zero;
            int nearby = 0;

            var group = _bot.BotsGroup;
            if (group != null)
            {
                int count = group.MembersCount;
                for (int i = 0; i < count; i++)
                {
                    var mate = group.Member(i);
                    if (mate == null || mate == _bot || mate.IsDead)
                        continue;

                    float dist = Vector3.Distance(_bot.Position, mate.Position);
                    if (dist < AvoidanceRadius && dist > 0.01f)
                    {
                        avoidance += (_bot.Position - mate.Position).normalized / dist;
                        nearby++;
                    }
                }
            }

            if (nearby > 0)
                avoidance = (avoidance / nearby).normalized * AvoidanceStrength;

            // === Compose final direction ===
            Vector3 final = baseDir + _chaosOffset + squadOffset + avoidance;
            final.y = 0f;

            return final.sqrMagnitude > 0.01f ? final.normalized : baseDir;
        }

        #endregion
    }
}
