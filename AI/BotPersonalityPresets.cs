#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI
{
    /// <summary>
    /// Provides runtime personality preset generation and lookup for all bots.
    /// </summary>
    public static class BotPersonalityPresets
    {
        #region Static Preset Cache

        /// <summary>
        /// Global preset lookup for each personality type.
        /// </summary>
        public static readonly Dictionary<PersonalityType, BotPersonalityProfile> Presets;

        static BotPersonalityPresets()
        {
            Presets = new Dictionary<PersonalityType, BotPersonalityProfile>();
            foreach (PersonalityType type in Enum.GetValues(typeof(PersonalityType)))
            {
                Presets[type] = GenerateProfile(type);
            }
        }

        #endregion

        #region Preset Generator

        /// <summary>
        /// Generates a personality profile based on the specified personality type.
        /// </summary>
        /// <param name="type">The personality type to generate a profile for.</param>
        /// <returns>A fully initialized bot personality profile.</returns>
        public static BotPersonalityProfile GenerateProfile(PersonalityType type)
        {
            var profile = new BotPersonalityProfile
            {
                Personality = type,
                PreferredMission = MissionBias.Random
            };

            switch (type)
            {
                case PersonalityType.Adaptive:
                    profile.Accuracy = 0.65f;
                    profile.Cohesion = 0.65f;
                    profile.RiskTolerance = 0.6f;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Aggressive:
                    profile.EngagementRange = 40f;
                    profile.Accuracy = 0.6f;
                    profile.RiskTolerance = 0.8f;
                    profile.AggressionLevel = 0.9f;
                    profile.FlinchThreshold = 0.2f;
                    profile.CommunicationLevel = 0.3f;
                    profile.PreferredMission = MissionBias.Fight;
                    break;

                case PersonalityType.Balanced:
                    profile.EngagementRange = 80f;
                    profile.Accuracy = 0.7f;
                    profile.RiskTolerance = 0.5f;
                    profile.AggressionLevel = 0.6f;
                    break;

                case PersonalityType.Camper:
                    profile.IsCamper = true;
                    profile.EngagementRange = 120f;
                    profile.Accuracy = 0.85f;
                    profile.FlinchThreshold = 0.3f;
                    profile.SuppressionSensitivity = 0.7f;
                    profile.PreferredMission = MissionBias.Loot;
                    break;

                case PersonalityType.Cautious:
                    profile.EngagementRange = 90f;
                    profile.Accuracy = 0.8f;
                    profile.RiskTolerance = 0.2f;
                    profile.AggressionLevel = 0.3f;
                    profile.Caution = 0.9f;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.ColdBlooded:
                    profile.Accuracy = 0.95f;
                    profile.AggressionLevel = 0.4f;
                    profile.Caution = 0.3f;
                    profile.SuppressionSensitivity = 0.1f;
                    profile.FlinchThreshold = 0.1f;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Defensive:
                    profile.EngagementRange = 100f;
                    profile.FlinchThreshold = 0.7f;
                    profile.RetreatThreshold = 0.5f;
                    profile.Caution = 0.7f;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Dumb:
                    profile.IsDumb = true;
                    profile.Accuracy = 0.3f;
                    profile.AggressionLevel = 0.2f;
                    profile.PreferredMission = MissionBias.Loot;
                    break;

                case PersonalityType.Explorer:
                    profile.EngagementRange = 110f;
                    profile.RiskTolerance = 0.9f;
                    profile.Caution = 0.3f;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Fearful:
                    profile.Accuracy = 0.4f;
                    profile.RetreatThreshold = 0.7f;
                    profile.Caution = 0.9f;
                    profile.AggressionLevel = 0.1f;
                    profile.IsFearful = true;
                    profile.PreferredMission = MissionBias.Loot;
                    break;

                case PersonalityType.Frenzied:
                    profile.AggressionLevel = 1.0f;
                    profile.ChaosFactor = 1.0f;
                    profile.IsFrenzied = true;
                    profile.AccuracyUnderFire = 0.2f;
                    profile.PreferredMission = MissionBias.Fight;
                    break;

                case PersonalityType.Loner:
                    profile.Cohesion = 0.0f;
                    profile.CommunicationLevel = 0.1f;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Patient:
                    profile.Accuracy = 0.85f;
                    profile.Caution = 0.8f;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Reckless:
                    profile.RiskTolerance = 1.0f;
                    profile.Accuracy = 0.4f;
                    profile.AggressionLevel = 1.0f;
                    profile.PreferredMission = MissionBias.Fight;
                    break;

                case PersonalityType.RiskTaker:
                    profile.RiskTolerance = 0.9f;
                    profile.EngagementRange = 60f;
                    profile.PreferredMission = MissionBias.Fight;
                    break;

                case PersonalityType.SilentHunter:
                    profile.IsSilentHunter = true;
                    profile.Accuracy = 0.9f;
                    profile.CommunicationLevel = 0.1f;
                    profile.FlinchThreshold = 0.2f;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Sniper:
                    profile.EngagementRange = 120f;
                    profile.Accuracy = 0.95f;
                    profile.FlinchThreshold = 0.2f;
                    profile.PreferredMission = MissionBias.Fight;
                    break;

                case PersonalityType.Strategic:
                    profile.RepositionPriority = 0.9f;
                    profile.AggressionLevel = 0.4f;
                    profile.Accuracy = 0.75f;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Stubborn:
                    profile.IsStubborn = true;
                    profile.RetreatThreshold = 0.0f;
                    profile.Cohesion = 0.2f;
                    profile.PreferredMission = MissionBias.Fight;
                    break;

                case PersonalityType.Tactical:
                    profile.FlinchThreshold = 0.5f;
                    profile.RepositionPriority = 1.0f;
                    profile.Caution = 0.6f;
                    profile.PreferredMission = MissionBias.Fight;
                    break;

                case PersonalityType.TeamPlayer:
                    profile.Cohesion = 1.0f;
                    profile.CommunicationLevel = 1.0f;
                    profile.IsTeamPlayer = true;
                    profile.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Unpredictable:
                    profile.ChaosFactor = 1.0f;
                    break;

                case PersonalityType.Vengeful:
                    profile.AggressionLevel = 0.9f;
                    profile.CommunicationLevel = 0.2f;
                    profile.RetreatThreshold = 0.1f;
                    profile.PreferredMission = MissionBias.Fight;
                    break;
            }

            return profile;
        }

        #endregion
    }
}
