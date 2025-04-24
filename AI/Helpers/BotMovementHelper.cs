#nullable enable

using AIRefactored.AI.Optimization;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Provides smooth, human-like movement and aim transitions for AIRefactored bots.
    /// Includes pathing, strafing, fallback, and gradual rotation logic.
    /// </summary>
    public static class BotMovementHelper
    {
        #region Constants

        private const float DefaultRadius = 0.8f;
        private const float DefaultLookSpeed = 4f;
        private const float DefaultStrafeDistance = 3f;

        #endregion

        #region Public Movement API

        /// <summary>
        /// Smoothly navigates the bot to a world-space position using GoToPoint.
        /// </summary>
        public static void SmoothMoveTo(BotOwner bot, Vector3 target, bool slow = true, float cohesionScale = 1.0f)
        {
            if (!IsEligible(bot))
                return;

            float buffer = DefaultRadius * Mathf.Clamp(cohesionScale, 0.7f, 1.3f);
            Vector3 pos = bot.Position;

            if ((pos - target).sqrMagnitude < buffer * buffer)
                return;

            bot.Mover.GoToPoint(target, slow, cohesionScale);
        }

        /// <summary>
        /// Smoothly rotates the bot’s body to face the specified world-space target.
        /// </summary>
        public static void SmoothLookTo(BotOwner bot, Vector3 lookTarget, float speed = DefaultLookSpeed)
        {
            if (!IsEligible(bot))
                return;

            Vector3 direction = lookTarget - bot.Position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            bot.Transform.rotation = Quaternion.Slerp(
                bot.Transform.rotation,
                targetRotation,
                Time.deltaTime * Mathf.Clamp(speed, 1f, 8f)
            );
        }

        /// <summary>
        /// Strafes the bot laterally from a known threat direction using a realistic sidestep.
        /// </summary>
        public static void SmoothStrafeFrom(BotOwner bot, Vector3 threatDirection, float scale = 1.0f)
        {
            if (!IsEligible(bot))
                return;

            Vector3 strafeDir = Vector3.Cross(Vector3.up, threatDirection.normalized);
            Vector3 offset = strafeDir * DefaultStrafeDistance * Mathf.Clamp(scale, 0.75f, 1.25f);
            Vector3 target = bot.Position + offset;

            bot.Mover.GoToPoint(
                pos: target,
                slowAtTheEnd: false,
                reachDist: 1f,
                getUpWithCheck: false,
                mustHaveWay: true,
                onlyShortTrie: false,
                force: false
            );
        }

        /// <summary>
        /// Retreats the bot toward cover using dynamic fallback scoring. Includes sprint flag.
        /// </summary>
        public static void RetreatToCover(BotOwner bot, Vector3 threatDirection)
        {
            if (!IsEligible(bot))
                return;

            Vector3 best = bot.Position - threatDirection.normalized * 6f;

            var cache = BotCacheUtility.GetCache(bot);
            if (cache != null && cache.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(bot, threatDirection, cache.PathCache);
                if (path.Count > 0)
                    best = path[path.Count - 1];
            }

            float cohesion = 1f;
            var profile = BotRegistry.TryGet(bot.ProfileId);
            if (profile != null)
                cohesion = Mathf.Clamp(profile.Cohesion, 0.7f, 1.3f);

            BotCoverHelper.MarkUsed(best);
            bot.Mover.GoToPoint(best, true, cohesion);
            bot.Sprint(true);
        }

        #endregion

        #region Validation

        private static bool IsEligible(BotOwner? bot)
        {
            return bot != null &&
                   bot.Mover != null &&
                   bot.GetPlayer != null &&
                   bot.GetPlayer.IsAI &&
                   !bot.IsDead;
        }

        #endregion

        #region Reset Stub

        public static void Reset(BotOwner bot)
        {
            // Reserved for future movement resets or AI interruption
        }

        #endregion
    }
}
