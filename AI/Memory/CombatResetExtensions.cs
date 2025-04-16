using EFT;

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
            if (bot == null || bot.Memory == null)
                return;

            // Signal bot is no longer panicking
            bot.Memory.IsPeace = false;

            // Trigger immediate re-engagement behavior
            bot.Memory.AttackImmediately = true;

            // Optional: force an update of the current state
            bot.Memory.CheckIsPeace();
        }
    }
}
