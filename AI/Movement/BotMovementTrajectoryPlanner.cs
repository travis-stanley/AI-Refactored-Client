#nullable enable

namespace AIRefactored.AI.Movement
{
    using System;

    using AIRefactored.AI.Core;

    using EFT;

    using UnityEngine;

    using Random = UnityEngine.Random;

    /// <summary>
    ///     Modifies a bot’s movement vector by applying chaos wobble, squad staggering offsets,
    ///     and teammate collision avoidance. Produces natural movement flow and reduces clustering.
    /// </summary>
    public sealed class BotMovementTrajectoryPlanner
    {
        private const float AvoidanceRadius = 2.0f;

        private const float AvoidanceScale = 1.25f;

        private const float ChaosInterval = 0.4f;

        private const float ChaosRadius = 0.65f;

        private const float SquadOffsetScale = 0.75f;

        private readonly BotOwner _bot;

        private readonly BotComponentCache _cache;

        private Vector3 _chaosOffset = Vector3.zero;

        private float _nextChaosUpdate;

        public BotMovementTrajectoryPlanner(BotOwner bot, BotComponentCache cache)
        {
            this._bot = bot ?? throw new ArgumentNullException(nameof(bot));
            this._cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        ///     Adjusts the input movement direction using chaos wobble, squad offset, and teammate avoidance.
        /// </summary>
        public Vector3 ModifyTrajectory(Vector3 targetDir, float deltaTime)
        {
            var now = Time.unscaledTime;

            if (now >= this._nextChaosUpdate) this.UpdateChaosOffset(now);

            var baseDir = targetDir.normalized;

            var offset = baseDir + this._chaosOffset;

            // Apply squad staggering offset
            if (this._cache.SquadPath is { } squadPath)
            {
                var squadOffset = squadPath.GetCurrentOffset();
                if (squadOffset.sqrMagnitude > 0.01f)
                    offset += squadOffset.normalized * SquadOffsetScale;
            }

            // Apply teammate avoidance
            var avoidance = this.ComputeAvoidance();
            if (avoidance.sqrMagnitude > 0.01f)
                offset += avoidance.normalized * AvoidanceScale;

            offset.y = 0f;
            return offset.sqrMagnitude > 0.01f ? offset.normalized : baseDir;
        }

        private Vector3 ComputeAvoidance()
        {
            var result = Vector3.zero;
            var count = 0;

            var group = this._bot.BotsGroup;
            if (group == null)
                return result;

            for (var i = 0; i < group.MembersCount; i++)
            {
                var mate = group.Member(i);
                if (mate == null || mate == this._bot || mate.IsDead)
                    continue;

                var dist = Vector3.Distance(this._bot.Position, mate.Position);
                if (dist < AvoidanceRadius && dist > 0.01f)
                {
                    result += (this._bot.Position - mate.Position).normalized / dist;
                    count++;
                }
            }

            return count > 0 ? result / count : Vector3.zero;
        }

        private void UpdateChaosOffset(float now)
        {
            var caution = this._cache.AIRefactoredBotOwner?.PersonalityProfile?.Caution ?? 0.5f;
            var chaosRange = ChaosRadius * (1f - caution);

            this._chaosOffset = new Vector3(
                Random.Range(-chaosRange, chaosRange),
                0f,
                Random.Range(-chaosRange, chaosRange));

            this._nextChaosUpdate = now + ChaosInterval;
        }
    }
}