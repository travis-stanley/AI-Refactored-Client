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
        /// Singleton instance of the internal optimizer.
        /// </summary>
        private static readonly BotAIOptimization _optimizer = new();

        #endregion

        #region Public API

        /// <summary>
        /// Applies AI optimization tuning for the specified bot if not already applied.
        /// </summary>
        /// <param name="bot">The bot instance to optimize.</param>
        public static void Apply(BotOwner bot)
        {
            _optimizer.Optimize(bot);
        }

        /// <summary>
        /// Resets the optimization flag for the specified bot, allowing reapplication.
        /// </summary>
        /// <param name="bot">The bot instance to reset optimization for.</param>
        public static void Reset(BotOwner bot)
        {
            _optimizer.ResetOptimization(bot);
        }

        #endregion
    }
}
