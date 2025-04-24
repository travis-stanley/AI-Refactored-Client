#nullable enable

using System.Diagnostics;

namespace AIRefactored.AI
{
    /// <summary>
    /// Defines a full personality configuration for AI bots, including behavior flags,
    /// tactical parameters, suppression response, and mission preferences.
    /// </summary>
    [DebuggerDisplay("{Personality} Aggro={AggressionLevel}, Acc={Accuracy}, Chaos={ChaosFactor}")]
    public class BotPersonalityProfile
    {
        #region Identity

        public PersonalityType Personality { get; set; } = PersonalityType.Balanced;
        public MissionBias PreferredMission { get; set; } = MissionBias.Random;

        #endregion

        #region Core Tactical Behavior

        public float EngagementRange { get; set; } = 80f;
        public float Accuracy { get; set; } = 0.7f;
        public float RepositionPriority { get; set; } = 0.8f;
        public float RiskTolerance { get; set; } = 0.5f;
        public float Cohesion { get; set; } = 0.75f;
        public float AggressionLevel { get; set; } = 0.6f;

        #endregion

        #region Perception & Suppression

        public float Caution { get; set; } = 0.5f;
        public float FlinchThreshold { get; set; } = 0.4f;
        public float SuppressionSensitivity { get; set; } = 0.4f;
        public float ReactionSpeed { get; set; } = 0.65f;
        public float ReactionTime { get; set; } = 0.25f;

        #endregion

        #region Advanced Behavior

        public float FlankBias { get; set; } = 0.5f;
        public float RetreatThreshold { get; set; } = 0.3f;
        public float SuppressiveFireBias { get; set; } = 0.2f;
        public float CommunicationLevel { get; set; } = 0.6f;
        public float ChaosFactor { get; set; } = 0f;
        public float AccuracyUnderFire { get; set; } = 0.4f;

        #endregion

        #region Leaning & Movement

        public LeanPreference LeaningStyle { get; set; } = LeanPreference.Conservative;
        public float MovementJitter { get; set; } = 0.2f;
        public float SideStepBias { get; set; } = 0.5f;
        public float LeanPeekFrequency { get; set; } = 0.5f;
        public float CornerCheckPauseTime { get; set; } = 0.35f;

        #endregion

        #region Behavior Flags

        public bool IsDumb { get; set; } = false;
        public bool IsFearful { get; set; } = false;
        public bool IsCamper { get; set; } = false;
        public bool IsFrenzied { get; set; } = false;
        public bool IsSilentHunter { get; set; } = false;
        public bool IsTeamPlayer { get; set; } = false;
        public bool IsSadistic { get; set; } = false;
        public bool IsStubborn { get; set; } = false;

        public bool CanSuppress => SuppressiveFireBias > 0.05f;
        public bool CanRetreat => RetreatThreshold > 0.05f;
        public bool CanFlank => FlankBias > 0.05f;

        #endregion

        #region Utilities

        /// <summary>Creates a shallow clone of this profile.</summary>
        public BotPersonalityProfile Clone() => (BotPersonalityProfile)MemberwiseClone();

        public override string ToString() =>
            $"[{Personality}] Aggro={AggressionLevel}, Acc={Accuracy}, Chaos={ChaosFactor}, Cohesion={Cohesion}";

        #endregion
    }

    #region Enums

    /// <summary>
    /// Personality types define high-level AI behavior identity and bias.
    /// </summary>
    public enum PersonalityType
    {
        Adaptive,
        Aggressive,
        Balanced,
        Camper,
        Cautious,
        ColdBlooded,
        Defensive,
        Dumb,
        Explorer,
        Fearful,
        Frenzied,
        Greedy,
        Heroic,
        Loner,
        Methodical,
        Paranoid,
        Patient,
        Reckless,
        RiskTaker,
        SilentHunter,
        Sniper,
        Strategic,
        Stubborn,
        Tactical,
        TeamPlayer,
        Unpredictable,
        Vengeful,
        Vigilant
    }

    /// <summary>
    /// Mission bias affects what the bot prefers to do on a match (loot, fight, quest).
    /// </summary>
    public enum MissionBias
    {
        Random,
        Loot,
        Fight,
        Quest
    }

    /// <summary>
    /// Defines how often bots lean from cover during peeking or scanning.
    /// </summary>
    public enum LeanPreference
    {
        Never,
        Conservative,
        Aggressive
    }

    #endregion
}
