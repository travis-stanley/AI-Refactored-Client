#nullable enable

using EFT;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Provides real-time, smooth movement helpers for bots.
    /// Includes pathing, strafing, and smooth aim/look-at rotation with human-like smoothing.
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
        /// Smoothly moves the bot toward a target world-space position.
        /// Uses cohesion-based spacing to avoid spam if already close.
        /// </summary>
        /// <param name="bot">The bot to move.</param>
        /// <param name="target">Target destination in world space.</param>
        /// <param name="slow">If true, bot walks/cautious moves.</param>
        /// <param name="cohesionScale">Optional cohesion buffer multiplier (default 1.0).</param>
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
        /// Smoothly rotates the bot to face a given target point.
        /// Uses Quaternion.Slerp for realistic natural turning.
        /// </summary>
        /// <param name="bot">The bot to rotate.</param>
        /// <param name="lookTarget">World-space position to look at.</param>
        /// <param name="speed">Rotation speed scalar.</param>
        public static void SmoothLookTo(BotOwner bot, Vector3 lookTarget, float speed = DefaultLookSpeed)
        {
            if (!IsEligible(bot))
                return;

            Vector3 direction = lookTarget - bot.Position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.01f)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            bot.Transform.rotation = Quaternion.Slerp(bot.Transform.rotation, targetRotation, Time.deltaTime * speed);
        }

        /// <summary>
        /// Evasive lateral movement away from threat direction.
        /// Strafes to the left/right from the perceived source of fire or aggression.
        /// </summary>
        /// <param name="bot">The bot to strafe.</param>
        /// <param name="threatDirection">Direction of perceived threat.</param>
        /// <param name="scale">Scale multiplier for strafe range.</param>
        public static void SmoothStrafeFrom(BotOwner bot, Vector3 threatDirection, float scale = 1.0f)
        {
            if (!IsEligible(bot))
                return;

            Vector3 strafeDir = Vector3.Cross(Vector3.up, threatDirection.normalized);
            Vector3 strafeOffset = strafeDir * DefaultStrafeDistance * Mathf.Clamp(scale, 0.5f, 1.5f);
            Vector3 strafeTarget = bot.Position + strafeOffset;

            bot.Mover.GoToPoint(strafeTarget, false, 1f); // always move fast during strafe
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns true if the bot is valid, AI-controlled, and has movement enabled.
        /// </summary>
        private static bool IsEligible(BotOwner? bot)
        {
            return bot != null &&
                   bot.Mover != null &&
                   bot.GetPlayer != null &&
                   bot.GetPlayer.IsAI &&
                   !bot.IsDead;
        }

        #endregion

        #region Legacy Compatibility

        /// <summary>
        /// Reserved hook for future reset logic if needed (compatibility stub).
        /// </summary>
        public static void Reset(BotOwner bot)
        {
            // Intentionally left empty for now.
        }

        #endregion
    }
}
