#nullable enable

using AIRefactored.AI.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Modifies a bot’s movement direction by applying personality-driven chaos, squad offset shaping,
    /// and teammate avoidance to prevent robotic behavior and improve natural navigation flow.
    /// </summary>
    public class BotMovementTrajectoryPlanner
    {
        #region Constants

        private const float ChaosInterval = 0.4f;
        private const float ChaosRadius = 0.65f;
        private const float AvoidanceRadius = 2.0f;
        private const float SquadOffsetStrength = 0.75f;
        private const float AvoidanceStrength = 1.25f;

        #endregion

        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;

        private Vector3 _chaosOffset = Vector3.zero;
        private float _nextChaosUpdate = 0f;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a movement planner tied to the bot’s personality and squad behavior.
        /// </summary>
        public BotMovementTrajectoryPlanner(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Modifies the given movement vector to apply chaos, spacing, and teammate collision avoidance.
        /// </summary>
        /// <param name="targetDir">The intended base movement direction.</param>
        /// <param name="deltaTime">Frame delta time.</param>
        /// <returns>A modified movement direction vector.</returns>
        public Vector3 ModifyTrajectory(Vector3 targetDir, float deltaTime)
        {
            float now = Time.time;

            // === Update random chaos offset ===
            if (now > _nextChaosUpdate)
            {
                _chaosOffset = new Vector3(
                    Random.Range(-ChaosRadius, ChaosRadius),
                    0f,
                    Random.Range(-ChaosRadius, ChaosRadius)
                );
                _nextChaosUpdate = now + ChaosInterval;
            }

            Vector3 baseDir = targetDir.normalized;

            // === Apply squad-based offset ===
            Vector3 squadOffset = _cache.SquadPath?.GetCurrentOffset() ?? Vector3.zero;
            if (squadOffset.sqrMagnitude > 0.01f)
                squadOffset = squadOffset.normalized * SquadOffsetStrength;

            // === Avoid nearby teammates ===
            Vector3 avoidance = Vector3.zero;
            int avoidCount = 0;

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
                        avoidCount++;
                    }
                }
            }

            if (avoidCount > 0)
                avoidance = (avoidance / avoidCount).normalized * AvoidanceStrength;

            // === Final movement vector ===
            Vector3 final = baseDir + _chaosOffset + squadOffset + avoidance;
            final.y = 0f;

            return final.sqrMagnitude > 0.001f ? final.normalized : baseDir;
        }

        #endregion
    }
}
