#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System.Collections.Generic;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Logs and verifies runtime bot AI tuning settings like vision, perception, and aggression.
    /// Used during development to confirm AI parameter consistency and per-bot diagnostics.
    /// </summary>
    public class BotAIOptimization
    {
        #region Fields

        /// <summary>
        /// Tracks which bots have had their optimization logged.
        /// Prevents duplicate logs or reapplication unless explicitly reset.
        /// </summary>
        private readonly Dictionary<string, bool> _optimizationApplied = new Dictionary<string, bool>(64);

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Logs and verifies the bot’s internal tuning parameters.
        /// Skips logging if the same bot has already been optimized this session.
        /// </summary>
        /// <param name="botOwner">Target bot to inspect.</param>
        public void Optimize(BotOwner botOwner)
        {
            if (!IsValidBot(botOwner))
                return;

            string botId = botOwner.Profile.Id;

            if (_optimizationApplied.TryGetValue(botId, out bool alreadyOptimized) && alreadyOptimized)
                return;

            LogVisionSettings(botOwner);
            LogMindSettings(botOwner);
            LogAggressionRole(botOwner);

            _optimizationApplied[botId] = true;
        }

        /// <summary>
        /// Clears the optimization flag for a bot, allowing logs to be re-generated.
        /// </summary>
        /// <param name="botOwner">Bot to reset log tracking for.</param>
        public void ResetOptimization(BotOwner botOwner)
        {
            if (!IsValidBot(botOwner))
                return;

            _optimizationApplied[botOwner.Profile.Id] = false;
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Confirms bot is alive, AI-controlled, and valid for optimization.
        /// </summary>
        private static bool IsValidBot(BotOwner? botOwner)
        {
            return botOwner != null &&
                   botOwner.GetPlayer != null &&
                   botOwner.GetPlayer.IsAI &&
                   !botOwner.IsDead &&
                   !botOwner.GetPlayer.IsYourPlayer;
        }

        /// <summary>
        /// Logs vision parameters such as grass penetration and lighting detection.
        /// </summary>
        private void LogVisionSettings(BotOwner bot)
        {
            var look = bot.Settings?.FileSettings?.Look;
            string name = bot.Profile.Info.Nickname;

            if (look != null)
            {
                _log.LogInfo($"[AIRefactored-Vision] {name} → GrassVision: {look.MAX_VISION_GRASS_METERS:F1}m | LightAdd: {look.ENEMY_LIGHT_ADD:F1}m");
            }
            else
            {
                _log.LogWarning($"[AIRefactored-Vision] {name} → Vision settings not found.");
            }
        }

        /// <summary>
        /// Logs aggression-related tuning from the bot's mind data.
        /// </summary>
        private void LogMindSettings(BotOwner bot)
        {
            var mind = bot.Settings?.FileSettings?.Mind;
            string name = bot.Profile.Info.Nickname;

            if (mind != null)
            {
                _log.LogInfo($"[AIRefactored-Mind] {name} → MinScare: {mind.MIN_DAMAGE_SCARE:F1} | RunOnDamageChance: {mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100}%");
            }
            else
            {
                _log.LogWarning($"[AIRefactored-Mind] {name} → Mind settings not found.");
            }
        }

        /// <summary>
        /// Logs the bot’s assigned AI role for debugging behavior routing.
        /// </summary>
        private void LogAggressionRole(BotOwner bot)
        {
            string name = bot.Profile.Info.Nickname;
            var role = bot.Profile.Info.Settings.Role;
            _log.LogInfo($"[AIRefactored-Aggression] {name} → Role: {role}");
        }

        #endregion
    }
}
