#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Manages runtime optimization routines for AI bots.
    /// Provides centralized access to performance tuning, reset, and escalation routines.
    /// Designed to improve tactical behavior and reduce simulation overhead.
    /// </summary>
    public static class AIOptimizationManager
    {
        #region Fields

        private static readonly BotAIOptimization _optimizer = new();
        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Applies baseline optimization settings to the specified bot.
        /// Should be called once after initialization to reduce overhead and optimize behavior cadence.
        /// </summary>
        public static void Apply(BotOwner? bot)
        {
            if (!IsValidBot(bot))
                return;

            var validBot = bot!; // Already validated
            _optimizer.Optimize(validBot);
        }

        /// <summary>
        /// Clears prior optimizations, allowing the bot to return to default pacing or be reoptimized.
        /// </summary>
        public static void Reset(BotOwner? bot)
        {
            if (!IsValidBot(bot))
                return;

            var validBot = bot!;
            _optimizer.ResetOptimization(validBot);
        }

        /// <summary>
        /// Escalates bot perception urgency and danger response timing in high-stimulus scenarios.
        /// Does not enhance vision, accuracy, or combat precision — only cognitive speed and urgency.
        /// </summary>
        public static void TriggerEscalation(BotOwner? bot)
        {
            if (!IsValidBot(bot))
                return;

            var validBot = bot!;
            var mind = validBot.Settings?.FileSettings?.Mind;

            if (mind != null)
            {
                mind.DIST_TO_FOUND_SQRT = Mathf.Clamp(mind.DIST_TO_FOUND_SQRT * 1.25f, 200f, 800f);
                mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(mind.ENEMY_LOOK_AT_ME_ANG * 0.7f, 5f, 60f);
                mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 = Mathf.Clamp(mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 + 25f, 0f, 100f);
            }

            Logger.LogInfo($"[AIRefactored] 🔺 Escalation triggered for bot: {BotName(validBot)}");
        }

        #endregion

        #region Helpers

        private static bool IsValidBot(BotOwner? bot)
        {
            return bot != null &&
                   bot.GetPlayer != null &&
                   bot.GetPlayer.IsAI &&
                   !bot.IsDead;
        }

        private static string BotName(BotOwner? bot)
        {
            return bot?.Profile?.Info?.Nickname ?? "Unknown";
        }

        #endregion
    }
}
