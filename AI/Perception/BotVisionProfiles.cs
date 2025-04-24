#nullable enable

using AIRefactored.AI.Helpers;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Provides vision profiles per <see cref="WildSpawnType"/>, with optional personality scaling.
    /// Used to simulate flash/flare/suppression resistance and light reactivity.
    /// </summary>
    public static class BotVisionProfiles
    {
        #region Defaults

        /// <summary>Fallback profile if no matching WildSpawnType or cache exists.</summary>
        private static readonly BotVisionProfile DefaultProfile = new()
        {
            AdaptationSpeed = 1f,
            LightSensitivity = 1f,
            AggressionResponse = 1f,
            MaxBlindness = 1f
        };

        #endregion

        #region Role Profiles

        private static readonly Dictionary<WildSpawnType, BotVisionProfile> Profiles = new()
        {
            [WildSpawnType.assault] = new() { AdaptationSpeed = 0.75f, LightSensitivity = 1.2f, AggressionResponse = 0.9f, MaxBlindness = 1.1f },
            [WildSpawnType.cursedAssault] = new() { AdaptationSpeed = 0.7f, LightSensitivity = 1.4f, AggressionResponse = 1.0f, MaxBlindness = 1.2f },
            [WildSpawnType.marksman] = new() { AdaptationSpeed = 1f, LightSensitivity = 1f, AggressionResponse = 1.1f, MaxBlindness = 1.1f },
            [WildSpawnType.sectantPriest] = new() { AdaptationSpeed = 0.5f, LightSensitivity = 1.5f, AggressionResponse = 0.5f, MaxBlindness = 1.3f },
            [WildSpawnType.sectantWarrior] = new() { AdaptationSpeed = 0.6f, LightSensitivity = 1.5f, AggressionResponse = 0.8f, MaxBlindness = 1.3f },
            [WildSpawnType.pmcBot] = new() { AdaptationSpeed = 2f, LightSensitivity = 0.85f, AggressionResponse = 1.4f, MaxBlindness = 0.8f },
            [WildSpawnType.exUsec] = new() { AdaptationSpeed = 1.9f, LightSensitivity = 0.85f, AggressionResponse = 1.4f, MaxBlindness = 0.85f },
            [WildSpawnType.bossBully] = new() { AdaptationSpeed = 1.3f, LightSensitivity = 1f, AggressionResponse = 2f, MaxBlindness = 1f },
            [WildSpawnType.followerBully] = new() { AdaptationSpeed = 1.1f, LightSensitivity = 1f, AggressionResponse = 1.7f, MaxBlindness = 1f },
            [WildSpawnType.bossKilla] = new() { AdaptationSpeed = 1.6f, LightSensitivity = 0.7f, AggressionResponse = 2.5f, MaxBlindness = 0.9f },
            [WildSpawnType.bossTagilla] = new() { AdaptationSpeed = 1.5f, LightSensitivity = 0.9f, AggressionResponse = 2.2f, MaxBlindness = 0.95f },
            [WildSpawnType.followerTagilla] = new() { AdaptationSpeed = 1.2f, LightSensitivity = 1f, AggressionResponse = 1.6f, MaxBlindness = 1f },
            [WildSpawnType.bossSanitar] = new() { AdaptationSpeed = 1.4f, LightSensitivity = 0.95f, AggressionResponse = 2f, MaxBlindness = 0.95f },
            [WildSpawnType.followerSanitar] = new() { AdaptationSpeed = 1.3f, LightSensitivity = 1f, AggressionResponse = 1.7f, MaxBlindness = 1f },
            [WildSpawnType.bossGluhar] = new() { AdaptationSpeed = 1.4f, LightSensitivity = 1f, AggressionResponse = 2.2f, MaxBlindness = 1f },
            [WildSpawnType.followerGluharAssault] = new() { AdaptationSpeed = 1.2f, LightSensitivity = 1f, AggressionResponse = 1.5f, MaxBlindness = 1f },
            [WildSpawnType.followerGluharScout] = new() { AdaptationSpeed = 1.3f, LightSensitivity = 1f, AggressionResponse = 1.7f, MaxBlindness = 1f },
            [WildSpawnType.followerGluharSecurity] = new() { AdaptationSpeed = 1.1f, LightSensitivity = 1.1f, AggressionResponse = 1.6f, MaxBlindness = 1f },
            [WildSpawnType.followerGluharSnipe] = new() { AdaptationSpeed = 1f, LightSensitivity = 1.1f, AggressionResponse = 1.4f, MaxBlindness = 1f },
            [WildSpawnType.bossKnight] = new() { AdaptationSpeed = 1.5f, LightSensitivity = 1f, AggressionResponse = 2f, MaxBlindness = 0.9f },
            [WildSpawnType.followerBigPipe] = new() { AdaptationSpeed = 1.2f, LightSensitivity = 1f, AggressionResponse = 1.8f, MaxBlindness = 0.95f },
            [WildSpawnType.followerBirdEye] = new() { AdaptationSpeed = 1.2f, LightSensitivity = 1.1f, AggressionResponse = 1.6f, MaxBlindness = 1f },
            [WildSpawnType.gifter] = new() { AdaptationSpeed = 1f, LightSensitivity = 0.8f, AggressionResponse = 0.5f, MaxBlindness = 1.1f },
            [WildSpawnType.arenaFighter] = new() { AdaptationSpeed = 1.3f, LightSensitivity = 1f, AggressionResponse = 1.5f, MaxBlindness = 0.95f }
        };

        #endregion

        #region Accessor

        /// <summary>
        /// Retrieves the bot vision profile, applying role-based defaults and optional personality blending.
        /// </summary>
        public static BotVisionProfile Get(Player bot)
        {
            if (bot.Profile?.Info?.Settings == null)
                return DefaultProfile;

            var role = bot.Profile.Info.Settings.Role;
            var baseProfile = Profiles.TryGetValue(role, out var p) ? p : DefaultProfile;

            var cache = BotCacheUtility.GetCache(bot);
            var personality = cache?.AIRefactoredBotOwner?.PersonalityProfile;

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
