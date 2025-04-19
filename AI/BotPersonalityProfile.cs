#nullable enable

using System.Diagnostics;

namespace AIRefactored.AI
{
    #region Enums

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

    public enum MissionBias
    {
        Random,
        Loot,
        Fight,
        Quest
    }

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

        public LeanPreference LeaningStyle { get; set; } = LeanPreference.Conservative;

        #endregion

        #region Motion Personality

        /// <summary>
        /// How much movement randomness this bot injects during travel. 0 = laser-straight, 1 = zigzag erratic.
        /// </summary>
        public float MovementJitter { get; set; } = 0.2f;

        /// <summary>
        /// Tendency to strafe right vs left during idle peeking.
        /// </summary>
        public float SideStepBias { get; set; } = 0.5f;

        /// <summary>
        /// Frequency (0-1) of lean peeking attempts near corners.
        /// </summary>
        public float LeanPeekFrequency { get; set; } = 0.5f;

        /// <summary>
        /// How long this bot typically pauses to lean-check a corner.
        /// </summary>
        public float CornerCheckPauseTime { get; set; } = 0.35f;

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
