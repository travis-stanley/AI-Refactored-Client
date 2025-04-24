#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Applies synchronized personality-based tuning across an AI squad.
    /// Enhances cohesion, group alert radius, and retaliation logic.
    /// </summary>
    public class BotOwnerGroupOptimization
    {
        #region Fields

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Applies group cohesion and perception modifiers to all valid AI bots in a squad.
        /// </summary>
        /// <param name="botOwners">List of bots in the same squad or group context.</param>
        public void OptimizeGroupAI(List<BotOwner>? botOwners)
        {
            if (botOwners is null || botOwners.Count == 0)
                return;

            for (int i = 0; i < botOwners.Count; i++)
            {
                BotOwner? bot = botOwners[i];
                if (bot?.GetPlayer is not { IsAI: true, IsYourPlayer: false } || bot.IsDead)
                    continue;

                var profile = bot.Profile;
                var settings = bot.Settings?.FileSettings?.Mind;

                if (profile?.Id is not string id || string.IsNullOrEmpty(id))
                    continue;

                BotPersonalityProfile? personality = BotRegistry.Get(id);
                if (personality == null || settings == null)
                    continue;

                ApplyModifiers(bot, personality, settings);
            }
        }

        #endregion

        #region Internal Logic

        /// <summary>
        /// Applies per-bot group modifiers to enhance reactivity and team cohesion based on personality.
        /// </summary>
        private void ApplyModifiers(BotOwner bot, BotPersonalityProfile profile, BotGlobalsMindSettings mind)
        {
            mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(300f, 600f, 1f - profile.Cohesion);
            mind.FRIEND_AGR_KILL = Mathf.Clamp(
                mind.FRIEND_AGR_KILL + profile.AggressionLevel * 0.15f,
                0f, 1f
            );
            mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(
                mind.ENEMY_LOOK_AT_ME_ANG - profile.Cohesion * 5f,
                5f, 30f
            );

            string name = bot.Profile?.Info?.Nickname ?? "UnknownBot";
            Logger.LogDebug($"[GroupOpt] {name} → Cohesion={profile.Cohesion:F2}, FRIEND_AGR_KILL={mind.FRIEND_AGR_KILL:F2}, ANG={mind.ENEMY_LOOK_AT_ME_ANG:F1}°");
        }

        /// <summary>
        /// Confirms that the target is a live AI-controlled bot and not the client player.
        /// </summary>
        private static bool IsValidAIBot(BotOwner? bot)
        {
            return bot is { IsDead: false } &&
                   bot.GetPlayer is { IsAI: true, IsYourPlayer: false };
        }

        #endregion
    }
}
