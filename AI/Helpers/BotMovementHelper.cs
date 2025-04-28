#nullable enable

namespace AIRefactored.AI.Helpers
{
    using AIRefactored.AI.Optimization;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Provides smooth, human-like movement and aim transitions for AIRefactored bots.
    ///     Includes pathing, strafing, fallback, and gradual rotation logic.
    /// </summary>
    public static class BotMovementHelper
    {
        private const float DefaultLookSpeed = 4f;

        private const float DefaultRadius = 0.8f;

        private const float DefaultStrafeDistance = 3f;

        private const float RetreatDistance = 6f;

        public static void Reset(BotOwner bot)
        {
            // Reserved for future movement resets or interruption logic.
        }

        /// <summary>
        ///     Retreats the bot toward cover using dynamic fallback scoring. Includes sprint flag.
        /// </summary>
        public static void RetreatToCover(
            BotOwner bot,
            Vector3 threatDirection,
            float distance = RetreatDistance,
            bool sprint = true)
        {
            if (!IsEligible(bot)) return;

            var fallback = bot.Position - threatDirection.normalized * distance;

            var cache = BotCacheUtility.GetCache(bot);
            if (cache?.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(bot, threatDirection, cache.PathCache);
                if (path.Count > 0)
                    fallback = path[path.Count - 1];
            }

            var cohesion = 1f;
            var profile = BotRegistry.TryGet(bot.ProfileId);
            if (profile != null)
                cohesion = Mathf.Clamp(profile.Cohesion, 0.7f, 1.3f);

            BotCoverHelper.MarkUsed(fallback);
            bot.Mover.GoToPoint(fallback, true, cohesion);

            if (sprint)
                bot.Sprint(true);
        }

        /// <summary>
        ///     Smoothly rotates the bot’s body to face the specified world-space target.
        /// </summary>
        public static void SmoothLookTo(BotOwner bot, Vector3 lookTarget, float speed = DefaultLookSpeed)
        {
            if (!IsEligible(bot) || bot.Transform == null)
                return;

            var direction = lookTarget - bot.Position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
                return;

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            bot.Transform.rotation = Quaternion.Slerp(
                bot.Transform.rotation,
                targetRotation,
                Time.deltaTime * Mathf.Clamp(speed, 1f, 8f));
        }

        /// <summary>
        ///     Smoothly navigates the bot to a world-space position using GoToPoint.
        /// </summary>
        public static void SmoothMoveTo(BotOwner bot, Vector3 target, bool slow = true, float cohesionScale = 1.0f)
        {
            if (!IsEligible(bot)) return;

            var buffer = DefaultRadius * Mathf.Clamp(cohesionScale, 0.7f, 1.3f);
            var pos = bot.Position;

            if ((pos - target).sqrMagnitude < buffer * buffer)
                return;

            bot.Mover.GoToPoint(target, slow, cohesionScale);
        }

        /// <summary>
        ///     Strafes the bot laterally from a known threat direction using a realistic sidestep.
        /// </summary>
        public static void SmoothStrafeFrom(BotOwner bot, Vector3 threatDirection, float scale = 1.0f)
        {
            if (!IsEligible(bot)) return;

            var safeDir = Vector3.Cross(Vector3.up, threatDirection.normalized);
            if (safeDir.sqrMagnitude < 0.01f)
                safeDir = Vector3.right;

            var offset = safeDir.normalized * DefaultStrafeDistance * Mathf.Clamp(scale, 0.75f, 1.25f);
            var target = bot.Position + offset;

            bot.Mover.GoToPoint(target, false, 1f);
        }

        private static bool IsEligible(BotOwner? bot)
        {
            return bot != null && !bot.IsDead && bot.GetPlayer?.IsAI == true && bot.Mover != null;
        }
    }
}