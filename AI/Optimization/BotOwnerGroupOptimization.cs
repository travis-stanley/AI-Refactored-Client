#nullable enable

using System.Collections.Generic;
using EFT;
using UnityEngine;
using AIRefactored.AI;
using AIRefactored.Core;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Applies group-aware AI tuning to a squad of bots using personality traits.
    /// Enhances cohesion, aggression scaling, and enemy awareness in coordinated AI.
    /// </summary>
    public class BotOwnerGroupOptimization
    {
        /// <summary>
        /// Optimizes AI values for a list of bots based on shared group logic.
        /// Affects panic distance, friend-kill aggression response, and visual threat angle.
        /// </summary>
        /// <param name="botOwners">The list of bots belonging to the same squad.</param>
        public void OptimizeGroupAI(List<BotOwner> botOwners)
        {
            for (int i = 0; i < botOwners.Count; i++)
            {
                var bot = botOwners[i];
                if (!IsAIBot(bot) || bot?.Profile == null || bot.Settings?.FileSettings?.Mind == null)
                    continue;

                var profile = BotRegistry.Get(bot.Profile.Id);
                if (profile == null)
                    continue;

                var mind = bot.Settings.FileSettings.Mind;

                // === Group Cohesion Enhancements ===

                // Expand enemy detection radius if bot is less cohesive (more independent)
                mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(300f, 600f, 1f - profile.Cohesion);

                // Scale aggression toward teammate killers based on personality
                mind.FRIEND_AGR_KILL = Mathf.Clamp(
                    mind.FRIEND_AGR_KILL + profile.AggressionLevel * 0.15f,
                    0f,
                    1f
                );

                // Narrow cone of visual detection to simulate more attentive scanning
                mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(
                    mind.ENEMY_LOOK_AT_ME_ANG - profile.Cohesion * 5f,
                    5f,
                    30f
                );
            }
        }

        /// <summary>
        /// Returns true only if the bot is AI-controlled and not a human or FIKA coop player.
        /// </summary>
        private static bool IsAIBot(BotOwner bot)
        {
            var player = bot?.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }
    }
}
