#nullable enable

namespace AIRefactored.AI
{
    using System;
    using System.Collections.Generic;

    using UnityEngine;

    using Random = UnityEngine.Random;

    /// <summary>
    ///     Provides randomized, personality-based bot profile presets.
    ///     Supports type-driven defaults blended with organic trait variance.
    /// </summary>
    public static class BotPersonalityPresets
    {
        /// <summary>
        ///     Cached personality preset mappings, generated at startup.
        /// </summary>
        public static readonly Dictionary<PersonalityType, BotPersonalityProfile> Presets;

        static BotPersonalityPresets()
        {
            var types = (PersonalityType[])Enum.GetValues(typeof(PersonalityType));
            Presets = new Dictionary<PersonalityType, BotPersonalityProfile>(types.Length);

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                Presets[type] = GenerateProfile(type);
            }
        }

        /// <summary>
        ///     Generates a randomized bot profile using base traits and blended variance.
        /// </summary>
        public static BotPersonalityProfile GenerateProfile(PersonalityType type)
        {
            var p = new BotPersonalityProfile
                        {
                            Personality = type,
                            PreferredMission = MissionBias.Random,
                            MovementJitter = 0.2f,
                            SideStepBias = 0.5f,
                            LeanPeekFrequency = 0.5f,
                            CornerCheckPauseTime = 0.35f
                        };

            switch (type)
            {
                case PersonalityType.Fearful:
                    p.IsFearful = true;
                    p.Accuracy = 0.4f;
                    p.RetreatThreshold = 0.7f;
                    p.Caution = 0.9f;
                    p.AggressionLevel = 0.1f;
                    p.PreferredMission = MissionBias.Loot;
                    break;

                case PersonalityType.Frenzied:
                    p.IsFrenzied = true;
                    p.AggressionLevel = 1f;
                    p.ChaosFactor = 1f;
                    p.Accuracy = 0.45f;
                    p.AccuracyUnderFire = 0.2f;
                    p.PreferredMission = MissionBias.Fight;
                    break;

                case PersonalityType.Methodical:
                    p.Caution = 0.75f;
                    p.MovementJitter = 0.1f;
                    p.LeanPeekFrequency = 0.7f;
                    p.CornerCheckPauseTime = 0.5f;
                    break;

                case PersonalityType.Vigilant:
                    p.ReactionSpeed = 1f;
                    p.ReactionTime = 0.15f;
                    p.FlinchThreshold = 0.65f;
                    break;

                case PersonalityType.Greedy:
                    p.RiskTolerance = 0.8f;
                    p.Cohesion = 0.4f;
                    p.PreferredMission = MissionBias.Loot;
                    break;

                case PersonalityType.Paranoid:
                    p.ReactionSpeed = 0.85f;
                    p.Caution = 0.95f;
                    p.FlinchThreshold = 0.9f;
                    p.SideStepBias = 0.6f;
                    break;

                case PersonalityType.Heroic:
                    p.AggressionLevel = 0.8f;
                    p.RiskTolerance = 0.85f;
                    p.CommunicationLevel = 1f;
                    p.Cohesion = 1f;
                    p.FlankBias = 0.6f;
                    break;

                case PersonalityType.Loner:
                    p.Cohesion = 0f;
                    p.CommunicationLevel = 0.1f;
                    break;

                case PersonalityType.Camper:
                    p.IsCamper = true;
                    p.EngagementRange = 120f;
                    p.Accuracy = 0.85f;
                    p.SuppressionSensitivity = 0.7f;
                    p.PreferredMission = MissionBias.Loot;
                    break;

                case PersonalityType.SilentHunter:
                    p.IsSilentHunter = true;
                    p.Accuracy = 0.95f;
                    p.CommunicationLevel = 0.1f;
                    p.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Dumb:
                    p.IsDumb = true;
                    p.Accuracy = 0.3f;
                    p.AggressionLevel = 0.2f;
                    p.ReactionSpeed = 0.25f;
                    break;

                case PersonalityType.Stubborn:
                    p.IsStubborn = true;
                    p.RetreatThreshold = 0f;
                    p.Cohesion = 0.2f;
                    break;

                case PersonalityType.Unpredictable:
                    p.ChaosFactor = 1f;
                    break;

                case PersonalityType.Vengeful:
                    p.AggressionLevel = 0.9f;
                    p.CommunicationLevel = 0.2f;
                    p.RetreatThreshold = 0.1f;
                    break;

                case PersonalityType.Sniper:
                    p.EngagementRange = 130f;
                    p.Accuracy = 0.95f;
                    p.FlinchThreshold = 0.2f;
                    break;

                case PersonalityType.Explorer:
                    p.EngagementRange = 110f;
                    p.RiskTolerance = 0.9f;
                    p.Caution = 0.3f;
                    p.PreferredMission = MissionBias.Quest;
                    break;

                case PersonalityType.Balanced:
                default:
                    p.Accuracy = 0.7f;
                    p.AggressionLevel = 0.6f;
                    p.Caution = 0.5f;
                    break;
            }

            ApplyRandomBlend(p);
            return p;
        }

        /// <summary>
        ///     Adds random variation to create behavioral diversity among same-type bots.
        /// </summary>
        private static void ApplyRandomBlend(BotPersonalityProfile p)
        {
            var chaos = p.ChaosFactor;
            var range = 0.1f + chaos * 0.3f;

            p.Accuracy += Random.Range(-0.1f, 0.15f);
            p.AggressionLevel += Random.Range(-0.1f, 0.15f);
            p.Cohesion += Random.Range(-0.2f, 0.2f);
            p.CommunicationLevel += Random.Range(-0.1f, 0.15f);
            p.MovementJitter += Random.Range(-0.1f, 0.15f);
            p.LeanPeekFrequency += Random.Range(-0.2f, 0.2f);
            p.CornerCheckPauseTime += Random.Range(-0.1f, 0.15f);
            p.SideStepBias += Random.Range(-0.15f, 0.2f);
            p.ReactionSpeed += Random.Range(-0.1f, 0.1f);
            p.ReactionTime += Random.Range(-0.05f, 0.1f);
            p.FlankBias += Random.Range(-0.1f, 0.15f);
            p.SuppressionSensitivity += Random.Range(-0.1f, 0.1f);
            p.FlinchThreshold += Random.Range(-0.1f, 0.1f);
            p.RetreatThreshold += Random.Range(-0.1f, 0.1f);
            p.RepositionPriority += Random.Range(-0.1f, 0.1f);
            p.RiskTolerance += Random.Range(-0.1f, 0.1f);

            // Clamp to valid 0–1 values where appropriate
            p.Accuracy = Mathf.Clamp01(p.Accuracy);
            p.AggressionLevel = Mathf.Clamp01(p.AggressionLevel);
            p.CommunicationLevel = Mathf.Clamp01(p.CommunicationLevel);
            p.Cohesion = Mathf.Clamp01(p.Cohesion);
            p.MovementJitter = Mathf.Clamp(p.MovementJitter, 0f, 0.5f);
            p.ReactionSpeed = Mathf.Clamp01(p.ReactionSpeed);
            p.ReactionTime = Mathf.Clamp(p.ReactionTime, 0.1f, 0.5f);
            p.SideStepBias = Mathf.Clamp01(p.SideStepBias);
            p.LeanPeekFrequency = Mathf.Clamp01(p.LeanPeekFrequency);
            p.FlankBias = Mathf.Clamp01(p.FlankBias);
            p.SuppressionSensitivity = Mathf.Clamp01(p.SuppressionSensitivity);
            p.FlinchThreshold = Mathf.Clamp01(p.FlinchThreshold);
            p.RetreatThreshold = Mathf.Clamp01(p.RetreatThreshold);
            p.RepositionPriority = Mathf.Clamp01(p.RepositionPriority);
            p.RiskTolerance = Mathf.Clamp01(p.RiskTolerance);
        }
    }
}