#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Applies group-aware AI tuning to a squad of bots using personality traits.
    /// Enhances cohesion, aggression scaling, and enemy awareness in coordinated AI.
    /// </summary>
    public class BotOwnerGroupOptimization
    {
        #region Logging

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;
        private static readonly bool _debug = false;

        #endregion

        #region Public API

        /// <summary>
        /// Optimizes AI values for a list of bots based on shared group logic.
        /// Affects friend-fire aggression response, enemy visual sensitivity, and cohesion-based tuning.
        /// </summary>
        /// <param name="botOwners">The squad of bots to optimize as a group.</param>
        public void OptimizeGroupAI(List<BotOwner>? botOwners)
        {
            if (botOwners == null || botOwners.Count == 0)
                return;

            for (int i = 0; i < botOwners.Count; i++)
            {
                BotOwner? bot = botOwners[i];
                if (!IsAIBot(bot))
                    continue;

                string? profileId = bot.Profile?.Id;
                if (string.IsNullOrEmpty(profileId))
                    continue;

                var profile = BotRegistry.Get(profileId!);

                var mind = bot.Settings?.FileSettings?.Mind as BotGlobalsMindSettings;
                if (mind == null)
                    continue;

                ApplyGroupModifiers(bot, profile, mind);
            }
        }

        #endregion

        #region Optimization Logic

        /// <summary>
        /// Applies group personality modifiers to an individual bot's mind config.
        /// </summary>
        /// <param name="bot">Target bot to adjust.</param>
        /// <param name="profile">Bot personality profile for tuning.</param>
        /// <param name="mind">Bot mind settings to modify.</param>
        private void ApplyGroupModifiers(BotOwner bot, BotPersonalityProfile profile, BotGlobalsMindSettings mind)
        {
            // Adjust group distance detection (larger for less cohesive squads)
            mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(300f, 600f, 1f - profile.Cohesion);

            // Increase aggression toward friend-killers based on bot’s aggression level
            mind.FRIEND_AGR_KILL = Mathf.Clamp(
                mind.FRIEND_AGR_KILL + profile.AggressionLevel * 0.15f,
                0f,
                1f
            );

            // Adjust enemy look-angle sensitivity (lower angle = reacts faster to enemy looking at them)
            mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(
                mind.ENEMY_LOOK_AT_ME_ANG - profile.Cohesion * 5f,
                5f,
                30f
            );

            if (_debug)
            {
                string name = bot.Profile?.Info?.Nickname ?? "Unknown Bot";
                Logger.LogDebug($"[AIRefactored-GroupOpt] ✔ {name} → " +
                                $"Cohesion={profile.Cohesion:F2}, FRIEND_AGR_KILL={mind.FRIEND_AGR_KILL:F2}, " +
                                $"ENEMY_LOOK_AT_ME_ANG={mind.ENEMY_LOOK_AT_ME_ANG:F2}");
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns true if this bot is AI-controlled (not human or coop).
        /// </summary>
        /// <param name="bot">BotOwner instance to check.</param>
        private static bool IsAIBot(BotOwner? bot)
        {
            Player? p = bot?.GetPlayer;
            return p != null && p.IsAI && !p.IsYourPlayer;
        }

        #endregion
    }
}
