#nullable enable

using EFT;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Centralized runtime-accessible manager for applying and resetting AI optimization.
    /// Wraps a singleton instance of <see cref="BotAIOptimization"/> and exposes simplified methods.
    /// </summary>
    public static class AIOptimizationManager
    {
        #region Fields

        /// <summary>
        /// Singleton instance of the internal AI optimizer.
        /// </summary>
        private static readonly BotAIOptimization _optimizer = new BotAIOptimization();

        #endregion

        #region Public API

        /// <summary>
        /// Applies runtime optimization tuning to the specified AI bot.
        /// Should be called once after bot initialization or escalation triggers.
        /// </summary>
        /// <param name="bot">The target BotOwner instance.</param>
        public static void Apply(BotOwner bot)
        {
            if (bot == null)
                return;

            _optimizer.Optimize(bot);
        }

        /// <summary>
        /// Resets any applied optimization flags for the bot, allowing re-application later.
        /// Useful when escalating or resetting bot behavior.
        /// </summary>
        /// <param name="bot">The BotOwner to reset optimization state for.</param>
        public static void Reset(BotOwner bot)
        {
            if (bot == null)
                return;

            _optimizer.ResetOptimization(bot);
        }

        #endregion
    }
}
