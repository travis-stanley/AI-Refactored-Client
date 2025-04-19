#nullable enable

using System.Diagnostics;

namespace AIRefactored.AI
{
    #region Enums

    /// <summary>
    /// Defines archetypal AI personalities used in AIRefactored.
    /// Each type influences tactical, behavioral, and decision-making traits.
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
        Loner,
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
        Vengeful
    }

    /// <summary>
    /// Represents the preferred mission objective bias used by bots to guide strategic decisions.
    /// </summary>
    public enum MissionBias
    {
        Random,
        Loot,
        Fight,
        Quest
    }

    /// <summary>
    /// Controls when and how bots use lean-to-aim tactics during combat.
    /// </summary>
    public enum LeanPreference
    {
        Never,
        Conservative,
        Aggressive
    }

    #endregion

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

        #region Perception & Suppression Tuning

        public float Caution { get; set; } = 0.5f;
        public float FlinchThreshold { get; set; } = 0.4f;
        public float SuppressionSensitivity { get; set; } = 0.4f;

        #endregion

        #region Advanced Behavior Modulation

        public float FlankBias { get; set; } = 0.5f;
        public float RetreatThreshold { get; set; } = 0.3f;
        public float SuppressiveFireBias { get; set; } = 0.2f;
        public float CommunicationLevel { get; set; } = 0.6f;
        public float ChaosFactor { get; set; } = 0.0f;
        public float AccuracyUnderFire { get; set; } = 0.4f;

        #endregion

        #region Leaning Behavior

        /// <summary>
        /// Controls whether and how the bot uses leaning in combat.
        /// </summary>
        public LeanPreference LeaningStyle { get; set; } = LeanPreference.Conservative;

        #endregion

        #region Behavior Modifiers (Flags)

        public bool IsDumb { get; set; } = false;
        public bool IsFearful { get; set; } = false;
        public bool IsCamper { get; set; } = false;
        public bool IsFrenzied { get; set; } = false;
        public bool IsSilentHunter { get; set; } = false;
        public bool IsTeamPlayer { get; set; } = false;
        public bool IsSadistic { get; set; } = false;
        public bool IsStubborn { get; set; } = false;

        #endregion
    }
}
