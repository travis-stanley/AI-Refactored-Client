#nullable enable

namespace AIRefactored.AI.Optimization
{
    using System.Collections.Generic;

    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using EFT;

    /// <summary>
    ///     Logs and verifies runtime bot AI settings like reaction thresholds, run chance, and role assignment.
    ///     Used during development to confirm bot configuration consistency and behavioral tuning.
    /// </summary>
    public class BotAIOptimization
    {
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        /// <summary>
        ///     Tracks bots already logged to avoid duplicate optimization prints.
        /// </summary>
        private readonly Dictionary<string, bool> _optimizationApplied = new(64);

        /// <summary>
        ///     Logs current optimization-relevant settings for the bot (once per bot ID).
        /// </summary>
        public void Optimize(BotOwner? botOwner)
        {
            if (!IsValidBot(botOwner))
                return;

            var bot = botOwner!;
            var botId = bot.Profile?.Id ?? string.Empty;

            if (string.IsNullOrEmpty(botId)
                || (this._optimizationApplied.TryGetValue(botId, out var already) && already))
                return;

            this.LogCognition(bot);
            this.LogMind(bot);
            this.LogRole(bot);

            this._optimizationApplied[botId] = true;
        }

        /// <summary>
        ///     Allows re-logging this bot by clearing its logged flag.
        /// </summary>
        public void ResetOptimization(BotOwner? botOwner)
        {
            if (!IsValidBot(botOwner))
                return;

            var bot = botOwner!;
            var botId = bot.Profile?.Id ?? string.Empty;
            if (!string.IsNullOrEmpty(botId)) this._optimizationApplied[botId] = false;
        }

        private static bool IsValidBot(BotOwner? botOwner)
        {
            return botOwner != null && botOwner.GetPlayer != null && botOwner.GetPlayer.IsAI
                   && !botOwner.GetPlayer.IsYourPlayer && !botOwner.IsDead;
        }

        private void LogCognition(BotOwner bot)
        {
            var look = bot.Settings?.FileSettings?.Look;
            var name = bot.Profile?.Info?.Nickname ?? "UnknownBot";

            if (look != null)
                _log.LogInfo(
                    $"[BotDiagnostics][Cognition] {name} → GrassVision={look.MAX_VISION_GRASS_METERS:F1}m (read-only), LightBonus={look.ENEMY_LIGHT_ADD:F1}m");
            else _log.LogWarning($"[BotDiagnostics][Cognition] {name} → No look config found.");
        }

        private void LogMind(BotOwner bot)
        {
            var mind = bot.Settings?.FileSettings?.Mind;
            var name = bot.Profile?.Info?.Nickname ?? "UnknownBot";

            if (mind != null)
                _log.LogInfo(
                    $"[BotDiagnostics][Mind] {name} → ScareThreshold={mind.MIN_DAMAGE_SCARE:F1}, RunChance={mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100:F0}%");
            else _log.LogWarning($"[BotDiagnostics][Mind] {name} → No mind config found.");
        }

        private void LogRole(BotOwner bot)
        {
            var name = bot.Profile?.Info?.Nickname ?? "UnknownBot";
            var role = bot.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault;

            _log.LogInfo($"[BotDiagnostics][Role] {name} → ProfileRole={role}");
        }
    }
}