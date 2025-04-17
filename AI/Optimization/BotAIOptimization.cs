#nullable enable

using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Logs and verifies runtime bot AI tuning settings like vision, perception, and aggression.
    /// Useful for diagnostics, mod testing, or live personality-based tuning.
    /// </summary>
    public class BotAIOptimization
    {
        #region State

        private readonly Dictionary<string, bool> _optimizationApplied = new();

        #endregion

        #region Public API

        /// <summary>
        /// Applies and logs optimization diagnostics for the given bot if not already applied.
        /// </summary>
        /// <param name="botOwner">The bot to optimize and verify.</param>
        public void Optimize(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            string botId = botOwner.Profile.Id;

            if (_optimizationApplied.TryGetValue(botId, out var alreadyOptimized) && alreadyOptimized)
                return;

            LogVisionSettings(botOwner);
            LogMindSettings(botOwner);
            LogAggressionRole(botOwner);

            _optimizationApplied[botId] = true;
        }

        /// <summary>
        /// Resets the optimization flag for the given bot, allowing reapplication.
        /// </summary>
        /// <param name="botOwner">The bot to reset.</param>
        public void ResetOptimization(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            _optimizationApplied[botOwner.Profile.Id] = false;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Returns true if the bot is AI-controlled and not a human or FIKA Coop player.
        /// </summary>
        private static bool IsAIBot(BotOwner botOwner)
        {
            var player = botOwner.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        /// <summary>
        /// Logs vision and perception settings from the Look config.
        /// </summary>
        private void LogVisionSettings(BotOwner bot)
        {
            var look = bot.Settings?.FileSettings?.Look;
            if (look != null)
            {
                Debug.Log($"[AIRefactored-Vision] {bot.Profile.Info.Nickname} → " +
                          $"GrassVision = {look.MAX_VISION_GRASS_METERS:F1}m, " +
                          $"LightAdd = {look.ENEMY_LIGHT_ADD:F1}m");
            }
        }

        /// <summary>
        /// Logs mind settings such as scare thresholds and reaction chance.
        /// </summary>
        private void LogMindSettings(BotOwner bot)
        {
            var mind = bot.Settings?.FileSettings?.Mind;
            if (mind != null)
            {
                Debug.Log($"[AIRefactored-Mind] {bot.Profile.Info.Nickname} → " +
                          $"MinScare = {mind.MIN_DAMAGE_SCARE:F1}, " +
                          $"RunOnDamageChance = {mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100}%");
            }
        }

        /// <summary>
        /// Logs the assigned WildSpawnType role of the bot.
        /// </summary>
        private void LogAggressionRole(BotOwner bot)
        {
            var role = bot.Profile.Info.Settings.Role;
            Debug.Log($"[AIRefactored-Aggression] {bot.Profile.Info.Nickname} → Role = {role}");
        }

        #endregion
    }
}
