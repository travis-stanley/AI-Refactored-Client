#nullable enable

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
        #region Public API

        /// <summary>
        /// Optimizes AI values for a list of bots based on shared group logic.
        /// Affects panic distance, friend-kill aggression response, and visual threat angle.
        /// </summary>
        /// <param name="botOwners">The list of bots belonging to the same squad.</param>
        public void OptimizeGroupAI(List<BotOwner> botOwners)
        {
            if (botOwners == null || botOwners.Count == 0)
                return;

            for (int i = 0; i < botOwners.Count; i++)
            {
                var bot = botOwners[i];
                if (!IsAIBot(bot))
                    continue;

                var profile = BotRegistry.Get(bot.Profile.Id);
                var mind = bot.Settings?.FileSettings?.Mind as BotGlobalsMindSettings;

                if (profile == null || mind == null)
                    continue;

                ApplyGroupModifiers(bot, profile, mind);
            }
        }

        #endregion

        #region Optimization Logic

        /// <summary>
        /// Applies group-based tuning to a bot's mind settings.
        /// </summary>
        private void ApplyGroupModifiers(BotOwner bot, BotPersonalityProfile profile, BotGlobalsMindSettings mind)
        {
            // Expand enemy detection distance based on low cohesion
            mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(300f, 600f, 1f - profile.Cohesion);

            // Increase aggression toward squad killers
            mind.FRIEND_AGR_KILL = Mathf.Clamp(
                mind.FRIEND_AGR_KILL + profile.AggressionLevel * 0.15f,
                0f,
                1f
            );

            // Narrow field-of-view angle with higher cohesion (simulates tighter scanning focus)
            mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(
                mind.ENEMY_LOOK_AT_ME_ANG - profile.Cohesion * 5f,
                5f,
                30f
            );

#if DEBUG
            Debug.Log($"[AIRefactored-GroupOpt] Optimized group logic for {bot.Profile?.Info?.Nickname} (Cohesion={profile.Cohesion:F2})");
#endif
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns true only if the bot is AI-controlled and not a human or FIKA coop player.
        /// </summary>
        private static bool IsAIBot(BotOwner? bot)
        {
            var player = bot?.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        #endregion
    }
}
