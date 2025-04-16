#nullable enable

using System.Collections.Generic;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Logs and verifies runtime bot AI tuning settings like vision, perception, and aggression.
    /// Used during initialization or optimization diagnostics.
    /// </summary>
    public class BotAIOptimization
    {
        private readonly Dictionary<string, bool> _optimizationApplied = new();

        public void Optimize(BotOwner botOwner)
        {
            if (botOwner?.Profile == null || botOwner.Memory == null)
                return;

            string botId = botOwner.Profile.Id;

            if (_optimizationApplied.TryGetValue(botId, out var alreadyOptimized) && alreadyOptimized)
                return;

            LogVisionSettings(botOwner);
            LogMindSettings(botOwner);
            LogAggressionRole(botOwner);

            _optimizationApplied[botId] = true;
        }

        public void ResetOptimization(BotOwner botOwner)
        {
            if (botOwner?.Profile == null)
                return;

            _optimizationApplied[botOwner.Profile.Id] = false;
        }

        private void LogVisionSettings(BotOwner bot)
        {
            var look = bot.Settings?.FileSettings?.Look;
            if (look != null)
            {
                Debug.Log($"[AIRefactored-Vision] Bot {bot.Profile.Info.Nickname} → GrassVision={look.MAX_VISION_GRASS_METERS:F1}m | LightAdd={look.ENEMY_LIGHT_ADD:F1}m");
            }
        }

        private void LogMindSettings(BotOwner bot)
        {
            var mind = bot.Settings?.FileSettings?.Mind;
            if (mind != null)
            {
                Debug.Log($"[AIRefactored-Mind] Bot {bot.Profile.Info.Nickname} → MinScare={mind.MIN_DAMAGE_SCARE:F1}, RunOnDamageChance={mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100}%");
            }
        }

        private void LogAggressionRole(BotOwner bot)
        {
            var role = bot.Profile.Info.Settings.Role;
            Debug.Log($"[AIRefactored-Aggression] Bot {bot.Profile.Info.Nickname} → Role={role}");
        }
    }
}
