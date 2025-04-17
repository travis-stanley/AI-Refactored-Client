#nullable enable

using UnityEngine;
using EFT;
using System;

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
        public static void SmoothMoveTo(BotOwner bot, Vector3 target, bool allowSlowEnd = true, float cohesionScale = 1.0f)
        {
            if (!IsBotEligible(bot)) return;

            float buffer = DefaultRadius * Mathf.Clamp(cohesionScale, 0.7f, 1.3f);
            Vector3 position = bot.Position;

            if ((position - target).sqrMagnitude < buffer * buffer)
                return;

            bot.Mover.GoToPoint(target, allowSlowEnd, cohesionScale);
        }

        /// <summary>
        /// Smoothly rotates the bot to face a direction or target position.
        /// Uses Lerp for human-like aim tracking.
        /// </summary>
        public static void SmoothLookTo(BotOwner bot, Vector3 lookTarget, float speed = DefaultLookSpeed)
        {
            if (!IsBotEligible(bot)) return;

            Vector3 direction = (lookTarget - bot.Position).normalized;
            if (direction == Vector3.zero) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            bot.Transform.rotation = Quaternion.Slerp(bot.Transform.rotation, targetRotation, Time.deltaTime * speed);
        }

        /// <summary>
        /// Issues a lateral movement order (strafe) away from a given direction.
        /// </summary>
        public static void SmoothStrafeFrom(BotOwner bot, Vector3 threatDirection, float scale = 1.0f)
        {
            if (!IsBotEligible(bot)) return;

            Vector3 side = Vector3.Cross(Vector3.up, threatDirection.normalized);
            Vector3 strafeTarget = bot.Position + side * DefaultStrafeDistance * Mathf.Clamp(scale, 0.5f, 1.5f);

            bot.Mover.GoToPoint(strafeTarget, slowAtTheEnd: false, reachDist: 1f, getUpWithCheck: true);
        }

        #endregion

        #region Helpers

        private static bool IsBotEligible(BotOwner? bot)
        {
            return bot != null && bot.Mover != null && !bot.IsDead && !BotCacheUtility.IsHumanPlayer(bot);
        }

        #endregion

        #region Reset (Retained for compatibility)

        public static void Reset(BotOwner bot)
        {
            // No-op: cooldown logic has been removed.
        }

        #endregion
    }
}
