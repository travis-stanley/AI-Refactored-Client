#nullable enable

using EFT;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Provides real-time, smooth movement logic for bots.
    /// Includes pathing, strafing, and smooth look-at behaviors.
    /// </summary>
    public static class BotMovementHelper
    {
        #region Constants

        private const float DefaultRadius = 0.8f;
        private const float DefaultLookSpeed = 4f;
        private const float DefaultStrafeDistance = 3f;

        #endregion

        #region Movement

        /// <summary>
        /// Smoothly moves the bot toward a world position using cohesion-based buffering.
        /// </summary>
        public static void SmoothMoveTo(BotOwner bot, Vector3 target, bool slow = true, float cohesionScale = 1.0f)
        {
            if (!IsEligible(bot))
                return;

            float buffer = DefaultRadius * Mathf.Clamp(cohesionScale, 0.7f, 1.3f);
            Vector3 position = bot.Position;

            if ((position - target).sqrMagnitude < buffer * buffer)
                return;

            bot.Mover.GoToPoint(target, slow, cohesionScale);
        }

        /// <summary>
        /// Smoothly rotates the bot to face a direction or target position.
        /// Uses Slerp for human-like aim tracking.
        /// </summary>
        public static void SmoothLookTo(BotOwner bot, Vector3 lookTarget, float speed = DefaultLookSpeed)
        {
            if (!IsEligible(bot))
                return;

            Vector3 dir = lookTarget - bot.Position;
            if (dir.sqrMagnitude < 0.01f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            bot.Transform.rotation = Quaternion.Slerp(bot.Transform.rotation, targetRotation, Time.deltaTime * speed);
        }

        /// <summary>
        /// Issues a lateral movement order (strafe) away from a given direction.
        /// </summary>
        public static void SmoothStrafeFrom(BotOwner bot, Vector3 threatDirection, float scale = 1.0f)
        {
            if (!IsEligible(bot))
                return;

            Vector3 right = Vector3.Cross(Vector3.up, threatDirection.normalized);
            Vector3 offset = right * DefaultStrafeDistance * Mathf.Clamp(scale, 0.5f, 1.5f);
            Vector3 strafeTarget = bot.Position + offset;

            bot.Mover.GoToPoint(strafeTarget, false, 1f); // false = not slow; cohesionScale = 1
        }

        #endregion

        #region Helpers

        private static bool IsEligible(BotOwner? bot)
        {
            return bot != null && bot.Mover != null && !bot.IsDead && bot.GetPlayer != null && bot.GetPlayer.IsAI;
        }

        #endregion

        #region Legacy Stub

        public static void Reset(BotOwner bot)
        {
            // No-op (for compatibility only)
        }

        #endregion
    }
}
