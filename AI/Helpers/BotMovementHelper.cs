#nullable enable

using UnityEngine;
using EFT;
using System;
using System.Collections.Generic;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Provides smooth movement logic for bots. Reduces snapping and improves realism by interpolating path targets.
    /// </summary>
    public static class BotMovementHelper
    {
        private const float DefaultRadius = 0.8f;
        private const float BaseCooldown = 0.15f;

        private static readonly Dictionary<BotOwner, float> LastMoveTime = new Dictionary<BotOwner, float>(64);

        /// <summary>
        /// Smooths bot movement with proximity buffering and cooldown.
        /// </summary>
        public static void SmoothMoveTo(BotOwner bot, Vector3 target, bool allowSlowEnd = true, float cohesionScale = 1.0f)
        {
            if (bot == null || bot.IsDead || bot.Mover == null)
                return;

            // 🛑 Ignore player-controlled entities
            if (BotCacheUtility.IsHumanPlayer(bot))
                return;

            float now = Time.time;
            float cooldown = BaseCooldown * Mathf.Clamp(cohesionScale, 0.5f, 2f);

            float lastMove;
            if (LastMoveTime.TryGetValue(bot, out lastMove) && (now - lastMove) < cooldown)
                return;

            LastMoveTime[bot] = now;

            float buffer = DefaultRadius * Mathf.Clamp(cohesionScale, 0.7f, 1.3f);
            Vector3 position = bot.Position;

            if ((position - target).sqrMagnitude < buffer * buffer)
                return;

            bot.Mover.GoToPoint(target, allowSlowEnd, cohesionScale);
        }

        /// <summary>
        /// Clears cooldown tracking for this bot.
        /// </summary>
        public static void Reset(BotOwner bot)
        {
            if (BotCacheUtility.IsHumanPlayer(bot))
                return;

            LastMoveTime.Remove(bot);
        }
    }
}
