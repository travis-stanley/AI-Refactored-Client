#nullable enable

using System;
using System.Collections.Generic;

namespace AIRefactored.AI
{
    /// <summary>
    /// Provides per-type preset templates for bot personalities.
    /// These are injected at spawn-time for dynamic, realistic variation across squads and maps.
    /// </summary>
    public static class BotPersonalityPresets
    {
        public static readonly Dictionary<PersonalityType, BotPersonalityProfile> Presets;

        private static readonly PersonalityType[] _types = (PersonalityType[])Enum.GetValues(typeof(PersonalityType));

        static BotPersonalityPresets()
        {
            Presets = new Dictionary<PersonalityType, BotPersonalityProfile>(_types.Length);
            for (int i = 0; i < _types.Length; i++)
            {
                PersonalityType type = _types[i];
                Presets[type] = GenerateProfile(type);
            }
        }

        /// <summary>
        /// Generates a bot personality profile for the given personality type.
        /// </summary>
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
                    profile.ReactionSpeed = 0.75f;
                    break;

                case PersonalityType.Aggressive:
                    profile.EngagementRange = 40f;
                    profile.Accuracy = 0.6f;
                    profile.RiskTolerance = 0.8f;
                    profile.AggressionLevel = 0.9f;
                    profile.FlinchThreshold = 0.2f;
                    profile.CommunicationLevel = 0.3f;
                    profile.PreferredMission = MissionBias.Fight;
                    profile.ReactionSpeed = 0.85f;
                    break;

                case PersonalityType.Balanced:
                    profile.EngagementRange = 80f;
                    profile.Accuracy = 0.7f;
                    profile.RiskTolerance = 0.5f;
                    profile.AggressionLevel = 0.6f;
                    profile.ReactionSpeed = 0.7f;
                    break;

                case PersonalityType.Camper:
                    profile.IsCamper = true;
                    profile.EngagementRange = 120f;
                    profile.Accuracy = 0.85f;
                    profile.FlinchThreshold = 0.3f;
                    profile.SuppressionSensitivity = 0.7f;
                    profile.PreferredMission = MissionBias.Loot;
                    profile.ReactionSpeed = 0.45f;
                    break;

                case PersonalityType.Cautious:
                    profile.EngagementRange = 90f;
                    profile.Accuracy = 0.8f;
                    profile.RiskTolerance = 0.2f;
                    profile.AggressionLevel = 0.3f;
                    profile.Caution = 0.9f;
                    profile.PreferredMission = MissionBias.Quest;
                    profile.ReactionSpeed = 0.5f;
                    break;

                case PersonalityType.ColdBlooded:
                    profile.Accuracy = 0.95f;
                    profile.AggressionLevel = 0.4f;
                    profile.Caution = 0.3f;
                    profile.SuppressionSensitivity = 0.1f;
                    profile.FlinchThreshold = 0.1f;
                    profile.PreferredMission = MissionBias.Quest;
                    profile.ReactionSpeed = 0.9f;
                    break;

                case PersonalityType.Defensive:
                    profile.EngagementRange = 100f;
                    profile.FlinchThreshold = 0.7f;
                    profile.RetreatThreshold = 0.5f;
                    profile.Caution = 0.7f;
                    profile.PreferredMission = MissionBias.Quest;
                    profile.ReactionSpeed = 0.55f;
                    break;

                case PersonalityType.Dumb:
                    profile.IsDumb = true;
                    profile.Accuracy = 0.3f;
                    profile.AggressionLevel = 0.2f;
                    profile.PreferredMission = MissionBias.Loot;
                    profile.ReactionSpeed = 0.25f;
                    break;

                case PersonalityType.Explorer:
                    profile.EngagementRange = 110f;
                    profile.RiskTolerance = 0.9f;
                    profile.Caution = 0.3f;
                    profile.PreferredMission = MissionBias.Quest;
                    profile.ReactionSpeed = 0.7f;
                    break;

                case PersonalityType.Fearful:
                    profile.IsFearful = true;
                    profile.Accuracy = 0.4f;
                    profile.RetreatThreshold = 0.7f;
                    profile.Caution = 0.9f;
                    profile.AggressionLevel = 0.1f;
                    profile.PreferredMission = MissionBias.Loot;
                    profile.ReactionSpeed = 0.4f;
                    break;

                case PersonalityType.Frenzied:
                    profile.IsFrenzied = true;
                    profile.AggressionLevel = 1.0f;
                    profile.ChaosFactor = 1.0f;
                    profile.AccuracyUnderFire = 0.2f;
                    profile.PreferredMission = MissionBias.Fight;
                    profile.ReactionSpeed = 1.0f;
                    break;

                case PersonalityType.Loner:
                    profile.Cohesion = 0.0f;
                    profile.CommunicationLevel = 0.1f;
                    profile.PreferredMission = MissionBias.Quest;
                    profile.ReactionSpeed = 0.6f;
                    break;

                case PersonalityType.Patient:
                    profile.Accuracy = 0.85f;
                    profile.Caution = 0.8f;
                    profile.PreferredMission = MissionBias.Quest;
                    profile.ReactionSpeed = 0.5f;
                    break;

                case PersonalityType.Reckless:
                    profile.RiskTolerance = 1.0f;
                    profile.Accuracy = 0.4f;
                    profile.AggressionLevel = 1.0f;
                    profile.PreferredMission = MissionBias.Fight;
                    profile.ReactionSpeed = 0.95f;
                    break;

                case PersonalityType.RiskTaker:
                    profile.RiskTolerance = 0.9f;
                    profile.EngagementRange = 60f;
                    profile.PreferredMission = MissionBias.Fight;
                    profile.ReactionSpeed = 0.8f;
                    break;

                case PersonalityType.SilentHunter:
                    profile.IsSilentHunter = true;
                    profile.Accuracy = 0.9f;
                    profile.CommunicationLevel = 0.1f;
                    profile.FlinchThreshold = 0.2f;
                    profile.PreferredMission = MissionBias.Quest;
                    profile.ReactionSpeed = 0.75f;
                    break;

                case PersonalityType.Sniper:
                    profile.EngagementRange = 120f;
                    profile.Accuracy = 0.95f;
                    profile.FlinchThreshold = 0.2f;
                    profile.PreferredMission = MissionBias.Fight;
                    profile.ReactionSpeed = 0.7f;
                    break;

                case PersonalityType.Strategic:
                    profile.RepositionPriority = 0.9f;
                    profile.AggressionLevel = 0.4f;
                    profile.Accuracy = 0.75f;
                    profile.PreferredMission = MissionBias.Quest;
                    profile.ReactionSpeed = 0.7f;
                    break;

                case PersonalityType.Stubborn:
                    profile.IsStubborn = true;
                    profile.RetreatThreshold = 0.0f;
                    profile.Cohesion = 0.2f;
                    profile.PreferredMission = MissionBias.Fight;
                    profile.ReactionSpeed = 0.6f;
                    break;

                case PersonalityType.Tactical:
                    profile.FlinchThreshold = 0.5f;
                    profile.RepositionPriority = 1.0f;
                    profile.Caution = 0.6f;
                    profile.PreferredMission = MissionBias.Fight;
                    profile.ReactionSpeed = 0.7f;
                    break;

                case PersonalityType.TeamPlayer:
                    profile.IsTeamPlayer = true;
                    profile.Cohesion = 1.0f;
                    profile.CommunicationLevel = 1.0f;
                    profile.PreferredMission = MissionBias.Quest;
                    profile.ReactionSpeed = 0.7f;
                    break;

                case PersonalityType.Unpredictable:
                    profile.ChaosFactor = 1.0f;
                    profile.ReactionSpeed = 0.75f;
                    break;

                case PersonalityType.Vengeful:
                    profile.AggressionLevel = 0.9f;
                    profile.CommunicationLevel = 0.2f;
                    profile.RetreatThreshold = 0.1f;
                    profile.PreferredMission = MissionBias.Fight;
                    profile.ReactionSpeed = 0.9f;
                    break;
            }

            // === Common motion values applied uniformly ===
            profile.MovementJitter = 0.2f;
            profile.SideStepBias = 0.5f;
            profile.LeanPeekFrequency = 0.5f;
            profile.CornerCheckPauseTime = 0.35f;

            return profile;
        }
    }
}
