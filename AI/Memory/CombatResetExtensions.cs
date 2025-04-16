using EFT;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Utility extensions for resetting bot memory and restoring aggression after panic/fallback.
    /// </summary>
    public static class CombatResetExtensions
    {
        /// <summary>
        /// Forces bot out of panic/fallback and re-evaluates combat state.
        /// </summary>
        public static void RestoreCombatAggression(this BotOwner bot)
        {
            if (bot == null || bot.Memory == null || bot.IsDead)
                return;

            // Signal bot is no longer in passive or fleeing mode
            bot.Memory.IsPeace = false;

            // Re-engage if enemy is known
            bot.Memory.AttackImmediately = true;

            // Refresh current combat state
            bot.Memory.CheckIsPeace();

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored-Combat] Bot {bot.Profile?.Info?.Nickname ?? "unknown"} restored to combat aggression.");
#endif
        }
    }
}
