#nullable enable

using UnityEngine;
using EFT;
using System;
using System.Collections.Generic;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Provides smooth movement logic for bots.
    /// Reduces snapping and improves realism by interpolating path targets with cooldown gating.
    /// </summary>
    public static class BotMovementHelper
    {
        #region Constants

        private const float DefaultRadius = 0.8f;
        private const float BaseCooldown = 0.15f;

        #endregion

        #region Internal State

        private static readonly Dictionary<BotOwner, float> LastMoveTime = new Dictionary<BotOwner, float>(64);

        #endregion

        #region Movement

        /// <summary>
        /// Moves the bot toward a target using distance smoothing and cooldown enforcement.
        /// Ignores player-controlled characters and respects cohesionScale for staggered squads.
        /// </summary>
        /// <param name="bot">Bot instance to move.</param>
        /// <param name="target">Target world position.</param>
        /// <param name="allowSlowEnd">Whether to allow deceleration near end-point.</param>
        /// <param name="cohesionScale">Multiplier for movement sensitivity and frequency.</param>
        public static void SmoothMoveTo(BotOwner bot, Vector3 target, bool allowSlowEnd = true, float cohesionScale = 1.0f)
        {
            if (bot == null || bot.IsDead || bot.Mover == null)
                return;

            if (BotCacheUtility.IsHumanPlayer(bot))
                return;

            float now = Time.time;
            float cooldown = BaseCooldown * Mathf.Clamp(cohesionScale, 0.5f, 2f);

            if (LastMoveTime.TryGetValue(bot, out float lastMove) && (now - lastMove) < cooldown)
                return;

            LastMoveTime[bot] = now;

            float buffer = DefaultRadius * Mathf.Clamp(cohesionScale, 0.7f, 1.3f);
            Vector3 position = bot.Position;

            if ((position - target).sqrMagnitude < buffer * buffer)
                return;

            bot.Mover.GoToPoint(target, allowSlowEnd, cohesionScale);
        }

        #endregion

        #region Reset

        /// <summary>
        /// Resets movement cooldown for the specified bot.
        /// </summary>
        /// <param name="bot">Bot to reset movement cooldown for.</param>
        public static void Reset(BotOwner bot)
        {
            if (BotCacheUtility.IsHumanPlayer(bot))
                return;

            LastMoveTime.Remove(bot);
        }

        #endregion
    }
}
