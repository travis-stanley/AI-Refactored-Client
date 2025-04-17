#nullable enable

using System.Collections.Generic;
using EFT;
using UnityEngine;
using AIRefactored.AI.Core;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Maps EFT WildSpawnTypes to default vision response profiles.
    /// Optionally overrides with personality-based scaling if available.
    /// </summary>
    public static class BotVisionProfiles
    {
        #region Fields

        private static readonly BotVisionProfile Default = new()
        {
            AdaptationSpeed = 1.0f,
            LightSensitivity = 1.0f,
            AggressionResponse = 1.0f,
            MaxBlindness = 1.0f
        };

        private static readonly Dictionary<WildSpawnType, BotVisionProfile> Profiles = new()
        {
            { WildSpawnType.assault, new() { AdaptationSpeed = 0.75f, LightSensitivity = 1.2f, AggressionResponse = 0.9f, MaxBlindness = 1.1f }},
            { WildSpawnType.cursedAssault, new() { AdaptationSpeed = 0.7f, LightSensitivity = 1.4f, AggressionResponse = 1.0f, MaxBlindness = 1.2f }},
            { WildSpawnType.marksman, new() { AdaptationSpeed = 1.0f, LightSensitivity = 1.0f, AggressionResponse = 1.1f, MaxBlindness = 1.1f }},
            { WildSpawnType.sectantPriest, new() { AdaptationSpeed = 0.5f, LightSensitivity = 1.5f, AggressionResponse = 0.5f, MaxBlindness = 1.3f }},
            { WildSpawnType.sectantWarrior, new() { AdaptationSpeed = 0.6f, LightSensitivity = 1.5f, AggressionResponse = 0.8f, MaxBlindness = 1.3f }},
            { WildSpawnType.pmcBot, new() { AdaptationSpeed = 2.0f, LightSensitivity = 0.85f, AggressionResponse = 1.4f, MaxBlindness = 0.8f }},
            { WildSpawnType.exUsec, new() { AdaptationSpeed = 1.9f, LightSensitivity = 0.85f, AggressionResponse = 1.4f, MaxBlindness = 0.85f }},
            { WildSpawnType.bossBully, new() { AdaptationSpeed = 1.3f, LightSensitivity = 1.0f, AggressionResponse = 2.0f, MaxBlindness = 1.0f }},
            { WildSpawnType.followerBully, new() { AdaptationSpeed = 1.1f, LightSensitivity = 1.0f, AggressionResponse = 1.7f, MaxBlindness = 1.0f }},
            { WildSpawnType.bossKilla, new() { AdaptationSpeed = 1.6f, LightSensitivity = 0.7f, AggressionResponse = 2.5f, MaxBlindness = 0.9f }},
            { WildSpawnType.bossTagilla, new() { AdaptationSpeed = 1.5f, LightSensitivity = 0.9f, AggressionResponse = 2.2f, MaxBlindness = 0.95f }},
            { WildSpawnType.followerTagilla, new() { AdaptationSpeed = 1.2f, LightSensitivity = 1.0f, AggressionResponse = 1.6f, MaxBlindness = 1.0f }},
            { WildSpawnType.bossSanitar, new() { AdaptationSpeed = 1.4f, LightSensitivity = 0.95f, AggressionResponse = 2.0f, MaxBlindness = 0.95f }},
            { WildSpawnType.followerSanitar, new() { AdaptationSpeed = 1.3f, LightSensitivity = 1.0f, AggressionResponse = 1.7f, MaxBlindness = 1.0f }},
            { WildSpawnType.bossGluhar, new() { AdaptationSpeed = 1.4f, LightSensitivity = 1.0f, AggressionResponse = 2.2f, MaxBlindness = 1.0f }},
            { WildSpawnType.followerGluharAssault, new() { AdaptationSpeed = 1.2f, LightSensitivity = 1.0f, AggressionResponse = 1.5f, MaxBlindness = 1.0f }},
            { WildSpawnType.followerGluharScout, new() { AdaptationSpeed = 1.3f, LightSensitivity = 1.0f, AggressionResponse = 1.7f, MaxBlindness = 1.0f }},
            { WildSpawnType.followerGluharSecurity, new() { AdaptationSpeed = 1.1f, LightSensitivity = 1.1f, AggressionResponse = 1.6f, MaxBlindness = 1.0f }},
            { WildSpawnType.followerGluharSnipe, new() { AdaptationSpeed = 1.0f, LightSensitivity = 1.1f, AggressionResponse = 1.4f, MaxBlindness = 1.0f }},
            { WildSpawnType.bossKnight, new() { AdaptationSpeed = 1.5f, LightSensitivity = 1.0f, AggressionResponse = 2.0f, MaxBlindness = 0.9f }},
            { WildSpawnType.followerBigPipe, new() { AdaptationSpeed = 1.2f, LightSensitivity = 1.0f, AggressionResponse = 1.8f, MaxBlindness = 0.95f }},
            { WildSpawnType.followerBirdEye, new() { AdaptationSpeed = 1.2f, LightSensitivity = 1.1f, AggressionResponse = 1.6f, MaxBlindness = 1.0f }},
            { WildSpawnType.gifter, new() { AdaptationSpeed = 1.0f, LightSensitivity = 0.8f, AggressionResponse = 0.5f, MaxBlindness = 1.1f }},
            { WildSpawnType.arenaFighter, new() { AdaptationSpeed = 1.3f, LightSensitivity = 1.0f, AggressionResponse = 1.5f, MaxBlindness = 0.95f }}
        };

        #endregion

        #region Public API

        /// <summary>
        /// Returns a composite vision profile based on wildspawn role and optional personality traits.
        /// </summary>
        public static BotVisionProfile Get(Player bot)
        {
            if (bot == null || bot.Profile == null || bot.AIData == null)
                return Default;

            var role = bot.Profile.Info.Settings?.Role ?? WildSpawnType.assault;
            var baseProfile = Profiles.TryGetValue(role, out var profile) ? profile : Default;

            var owner = bot.GetComponent<AIRefactoredBotOwner>();
            var personality = owner?.PersonalityProfile;

            if (personality == null)
                return baseProfile;

            return new BotVisionProfile
            {
                AdaptationSpeed = Mathf.Clamp(baseProfile.AdaptationSpeed + (1f - personality.Caution) * 0.5f, 0.5f, 3f),
                MaxBlindness = Mathf.Clamp(baseProfile.MaxBlindness + (1f - personality.RiskTolerance) * 0.4f, 0.5f, 2f),
                LightSensitivity = Mathf.Clamp(baseProfile.LightSensitivity + personality.Caution * 0.5f, 0.3f, 2f),
                AggressionResponse = Mathf.Clamp(baseProfile.AggressionResponse + personality.AggressionLevel * 0.5f, 0.5f, 3f)
            };
        }

        #endregion
    }
}
