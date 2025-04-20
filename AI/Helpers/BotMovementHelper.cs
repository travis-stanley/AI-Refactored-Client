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

        private static readonly bool EnableDebug = false;

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

            // If the bot is close enough to the target, skip movement
            if ((position - target).sqrMagnitude < buffer * buffer)
                return;

            if (EnableDebug)
                Debug.DrawLine(position, target, Color.green, 0.1f);

            // Issue the movement command with adjusted speed based on cohesion
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

            // Rotate bot smoothly towards the target using spherical interpolation
            Quaternion targetRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            bot.Transform.rotation = Quaternion.Slerp(bot.Transform.rotation, targetRotation, Time.deltaTime * speed);

            if (EnableDebug)
                Debug.DrawRay(bot.Position + Vector3.up * 1.5f, dir.normalized * 2f, Color.yellow, 0.05f);
        }

        /// <summary>
        /// Issues a lateral movement order (strafe) away from a given direction.
        /// </summary>
        public static void SmoothStrafeFrom(BotOwner bot, Vector3 threatDirection, float scale = 1.0f)
        {
            if (!IsEligible(bot))
                return;

            // Calculate a strafe direction perpendicular to the threat direction
            Vector3 right = Vector3.Cross(Vector3.up, threatDirection.normalized);
            Vector3 offset = right * DefaultStrafeDistance * Mathf.Clamp(scale, 0.5f, 1.5f);
            Vector3 strafeTarget = bot.Position + offset;

            if (EnableDebug)
                Debug.DrawLine(bot.Position, strafeTarget, Color.cyan, 0.1f);

            // Move the bot to the calculated strafe target
            bot.Mover.GoToPoint(strafeTarget, false, 1f); // fast movement
        }

        #endregion

        #region Helpers

        private static bool IsEligible(BotOwner? bot)
        {
            // Check if the bot is valid, not dead, and is AI-controlled
            return bot != null && bot.Mover != null && !bot.IsDead && bot.GetPlayer?.IsAI == true;
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
