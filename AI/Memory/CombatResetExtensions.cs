#nullable enable

using EFT;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Provides tactical memory reset tools for bots following panic, fallback, or disengagement.
    /// </summary>
    public static class CombatResetExtensions
    {
        #region Public API

        /// <summary>
        /// Resets bot's memory to a combat-ready state — used after fallback, suppression, or confusion.
        /// </summary>
        /// <param name="bot">The BotOwner instance to reset.</param>
        public static void RestoreCombatAggression(this BotOwner bot)
        {
            if (bot == null || bot.IsDead || bot.Memory == null)
                return;

            bot.Memory.IsPeace = false;
            bot.Memory.AttackImmediately = true;
            bot.Memory.CheckIsPeace();

            Debug.Log($"[AIRefactored-Combat] Bot {bot.Profile?.Info?.Nickname ?? "unknown"} restored to combat aggression.");
        }

        #endregion
    }
}
