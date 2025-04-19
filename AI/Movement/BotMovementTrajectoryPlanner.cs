#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Adds randomized chaos offset and squad-aware shaping to prevent robotic or clumped bot movement.
    /// </summary>
    public class BotMovementTrajectoryPlanner
    {
        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;

        private Vector3 _chaosOffset = Vector3.zero;
        private float _nextOffsetTime = 0f;

        private const float ChaosInterval = 0.4f;
        private const float ChaosRadius = 0.65f;
        private const float AvoidanceRadius = 2.0f;

        public BotMovementTrajectoryPlanner(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
        }

        /// <summary>
        /// Modifies the bot’s intended path with chaos + squad shaping + collision avoidance.
        /// </summary>
        public Vector3 ModifyTrajectory(Vector3 targetDir, float deltaTime)
        {
            float now = Time.time;

            // === Chaos offset update
            if (now > _nextOffsetTime)
            {
                _chaosOffset = new Vector3(
                    UnityEngine.Random.Range(-ChaosRadius, ChaosRadius),
                    0f,
                    UnityEngine.Random.Range(-ChaosRadius, ChaosRadius)
                );
                _nextOffsetTime = now + ChaosInterval;
            }

            Vector3 baseDirection = targetDir.normalized;

            // === Squad spacing (formation)
            Vector3 squadOffset = Vector3.zero;
            if (_cache.GetComponent<SquadPathCoordinator>() is SquadPathCoordinator squad)
                squadOffset = squad.GetCurrentOffset();

            // === Teammate avoidance (repulsion vector)
            Vector3 avoidance = Vector3.zero;
            if (_bot.BotsGroup != null)
            {
                for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
                {
                    var mate = _bot.BotsGroup.Member(i);
                    if (mate == null || mate == _bot || mate.IsDead)
                        continue;

                    float dist = Vector3.Distance(_bot.Position, mate.Position);
                    if (dist < AvoidanceRadius && dist > 0.01f)
                    {
                        avoidance += (_bot.Position - mate.Position).normalized / dist;
                    }
                }
            }

            // === Combine all movement influences
            Vector3 finalDirection = baseDirection
                + _chaosOffset
                + squadOffset.normalized * 0.5f
                + avoidance * 1.25f;

            finalDirection.y = 0f;

            return finalDirection.sqrMagnitude > 0.01f
                ? finalDirection.normalized
                : baseDirection;
        }
    }
}
