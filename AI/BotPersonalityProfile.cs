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

        /// <summary> Max distance bot will willingly engage enemies. </summary>
        public float EngagementRange { get; set; } = 80f;

        /// <summary> Base accuracy modifier. </summary>
        public float Accuracy { get; set; } = 0.7f;

        /// <summary> Priority of seeking new positions when in combat. </summary>
        public float RepositionPriority { get; set; } = 0.8f;

        /// <summary> Willingness to engage risky situations. </summary>
        public float RiskTolerance { get; set; } = 0.5f;

        /// <summary> Squad cohesion factor. Higher = sticks close to group. </summary>
        public float Cohesion { get; set; } = 0.75f;

        /// <summary> Aggression level: how readily it pushes, rushes, or baits. </summary>
        public float AggressionLevel { get; set; } = 0.6f;

        #endregion

        #region Perception & Suppression

        /// <summary> Bot’s tendency to investigate suspicious sound. </summary>
        public float Caution { get; set; } = 0.5f;

        /// <summary> How easily the bot flinches under stress. </summary>
        public float FlinchThreshold { get; set; } = 0.4f;

        /// <summary> How easily suppression/panic is triggered. </summary>
        public float SuppressionSensitivity { get; set; } = 0.4f;

        /// <summary> Speed at which the bot reacts to stimuli (0 = slow, 1 = instant). </summary>
        public float ReactionSpeed { get; set; } = 0.65f;

        #endregion

        #region Advanced Behavior

        /// <summary> Tendency to flank rather than brute force. </summary>
        public float FlankBias { get; set; } = 0.5f;

        /// <summary> HP % threshold below which bot tries to retreat. </summary>
        public float RetreatThreshold { get; set; } = 0.3f;

        /// <summary> Likelihood bot suppresses rather than aims directly. </summary>
        public float SuppressiveFireBias { get; set; } = 0.2f;

        /// <summary> Willingness to communicate events to squad. </summary>
        public float CommunicationLevel { get; set; } = 0.6f;

        /// <summary> Chaos/random factor influencing aim, behavior, and positioning. </summary>
        public float ChaosFactor { get; set; } = 0.0f;

        /// <summary> Accuracy penalty mitigation while under fire. </summary>
        public float AccuracyUnderFire { get; set; } = 0.4f;

        #endregion

        #region Leaning & Movement

        public LeanPreference LeaningStyle { get; set; } = LeanPreference.Conservative;

        /// <summary> Movement wobble/randomness (0 = smooth, 1 = erratic). </summary>
        public float MovementJitter { get; set; } = 0.2f;

        /// <summary> Bias toward right or left strafing. </summary>
        public float SideStepBias { get; set; } = 0.5f;

        /// <summary> Frequency of lean-peek attempts near cover or corners. </summary>
        public float LeanPeekFrequency { get; set; } = 0.5f;

        /// <summary> Time bot pauses when scanning a corner. </summary>
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

        public BotPersonalityProfile Clone()
        {
            return (BotPersonalityProfile)MemberwiseClone();
        }

        public override string ToString()
        {
            return $"[{Personality}] Aggro={AggressionLevel}, Acc={Accuracy}, Chaos={ChaosFactor}, Cohesion={Cohesion}";
        }

        #endregion
    }
}
