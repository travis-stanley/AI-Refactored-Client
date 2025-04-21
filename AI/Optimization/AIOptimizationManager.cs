#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Manages runtime optimization routines for AI bots.
    /// Provides centralized access to optimization, reset, and escalation routines.
    /// Designed to reduce resource use and improve tactical realism at runtime.
    /// </summary>
    public static class AIOptimizationManager
    {
        #region Fields

        private static readonly BotAIOptimization _optimizer = new();
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Applies baseline optimization settings to the specified bot.
        /// Should be called once after the bot is initialized to improve performance and decision pacing.
        /// </summary>
        /// <param name="bot">BotOwner to optimize.</param>
        public static void Apply(BotOwner bot)
        {
            if (!IsValidBot(bot))
                return;

            _optimizer.Optimize(bot);
        }

        /// <summary>
        /// Clears previous optimizations so they may be reapplied or escalated.
        /// </summary>
        /// <param name="bot">BotOwner to reset optimization state for.</param>
        public static void Reset(BotOwner bot)
        {
            if (!IsValidBot(bot))
                return;

            _optimizer.ResetOptimization(bot);
        }

        /// <summary>
        /// Escalates the bot’s tuning in response to intense or repeated stimuli (e.g. ambush, suppression, panic).
        /// Adjusts vision, recoil, and behavioral thresholds for heightened response.
        /// </summary>
        /// <param name="bot">BotOwner to escalate tuning for.</param>
        public static void TriggerEscalation(BotOwner bot)
        {
            if (!IsValidBot(bot))
                return;

            var shoot = bot.Settings?.FileSettings?.Shoot;
            var look = bot.Settings?.FileSettings?.Look;
            var mind = bot.Settings?.FileSettings?.Mind;

            if (shoot != null)
                shoot.RECOIL_PER_METER = Mathf.Clamp(shoot.RECOIL_PER_METER * 0.85f, 0.1f, 2f);

            if (look != null)
                look.MAX_VISION_GRASS_METERS = Mathf.Clamp(look.MAX_VISION_GRASS_METERS + 5f, 5f, 40f);

            if (mind != null)
            {
                mind.DIST_TO_FOUND_SQRT = Mathf.Clamp(mind.DIST_TO_FOUND_SQRT * 1.2f, 200f, 800f);
                mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(mind.ENEMY_LOOK_AT_ME_ANG * 0.75f, 5f, 45f);
                mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 = Mathf.Clamp(mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 + 20f, 0f, 100f);
            }

            _log.LogInfo($"[AIRefactored] 🔺 Escalation tuning applied to bot: {bot.Profile?.Info?.Nickname ?? "Unknown"}");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Ensures the bot is valid, alive, and AI-controlled before optimization.
        /// </summary>
        private static bool IsValidBot(BotOwner? bot)
        {
            return bot != null &&
                   bot.GetPlayer != null &&
                   bot.GetPlayer.IsAI &&
                   !bot.IsDead;
        }

        #endregion
    }
}
