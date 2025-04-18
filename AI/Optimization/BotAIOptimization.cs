#nullable enable

using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Logs and verifies runtime bot AI tuning settings like vision, perception, and aggression.
    /// Useful for diagnostics, mod testing, or live personality-based tuning.
    /// </summary>
    public class BotAIOptimization
    {
        #region Fields

        /// <summary>
        /// Tracks whether optimization has already been applied to a bot by ID.
        /// Prevents redundant logging or tuning reapplication.
        /// </summary>
        private readonly Dictionary<string, bool> _optimizationApplied = new Dictionary<string, bool>();

        #endregion

        #region Public API

        /// <summary>
        /// Applies runtime diagnostics and logs AI tuning settings for the bot.
        /// Skips reapplication if already optimized.
        /// </summary>
        /// <param name="botOwner">The bot to inspect and tune.</param>
        public void Optimize(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            string botId = botOwner.Profile.Id;

            bool alreadyOptimized;
            if (_optimizationApplied.TryGetValue(botId, out alreadyOptimized) && alreadyOptimized)
                return;

            LogVisionSettings(botOwner);
            LogMindSettings(botOwner);
            LogAggressionRole(botOwner);

            _optimizationApplied[botId] = true;
        }

        /// <summary>
        /// Clears the optimization flag for a bot, allowing reapplication on next call to Optimize().
        /// </summary>
        /// <param name="botOwner">The bot to reset.</param>
        public void ResetOptimization(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            string botId = botOwner.Profile.Id;
            _optimizationApplied[botId] = false;
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Returns true if the bot is AI-controlled and not a human-controlled player.
        /// </summary>
        private static bool IsAIBot(BotOwner botOwner)
        {
            var player = botOwner.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        /// <summary>
        /// Logs grass vision range and light boost radius for enemy detection.
        /// </summary>
        private void LogVisionSettings(BotOwner bot)
        {
            var look = bot.Settings?.FileSettings?.Look;
            if (look != null)
            {
                Debug.Log($"[AIRefactored-Vision] {bot.Profile.Info.Nickname} → " +
                          $"GrassVision: {look.MAX_VISION_GRASS_METERS:F1}m | " +
                          $"LightAdd: {look.ENEMY_LIGHT_ADD:F1}m");
            }
        }

        /// <summary>
        /// Logs psychological response parameters like fear thresholds and damage-based reaction chance.
        /// </summary>
        private void LogMindSettings(BotOwner bot)
        {
            var mind = bot.Settings?.FileSettings?.Mind;
            if (mind != null)
            {
                Debug.Log($"[AIRefactored-Mind] {bot.Profile.Info.Nickname} → " +
                          $"MinScare: {mind.MIN_DAMAGE_SCARE:F1} | " +
                          $"RunOnDamageChance: {mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100}%");
            }
        }

        /// <summary>
        /// Logs the bot’s assigned role based on the WildSpawnType (e.g., pmcUSEC, assault).
        /// </summary>
        private void LogAggressionRole(BotOwner bot)
        {
            var role = bot.Profile.Info.Settings.Role;
            Debug.Log($"[AIRefactored-Aggression] {bot.Profile.Info.Nickname} → Role: {role}");
        }

        #endregion
    }
}
