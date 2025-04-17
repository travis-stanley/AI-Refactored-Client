#nullable enable

using System.Diagnostics;

namespace AIRefactored.AI
{
    #region Enums

    /// <summary>
    /// Defines all supported bot personality archetypes used in AIRefactored.
    /// Each personality influences different aspects of tactical behavior.
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
    /// Defines a preferred mission type for behavior-driven bot objectives.
    /// </summary>
    public enum MissionBias
    {
        Random,
        Loot,
        Fight,
        Quest
    }

    #endregion

    /// <summary>
    /// AI personality profile used throughout AIRefactored to configure bot tactical behavior,
    /// perception response, team cohesion, and risk tolerance.
    /// </summary>
    [DebuggerDisplay("{Personality} Aggro={AggressionLevel}, Acc={Accuracy}, Chaos={ChaosFactor}")]
    public class BotPersonalityProfile
    {
        #region Identity

        /// <summary>
        /// Primary personality archetype label (used for debugging and generation).
        /// </summary>
        public PersonalityType Personality { get; set; } = PersonalityType.Balanced;

        /// <summary>
        /// Optional mission preference to influence movement, targeting, or goal logic.
        /// </summary>
        public MissionBias PreferredMission { get; set; } = MissionBias.Random;

        #endregion

        #region Core Tactical Behavior

        /// <summary>Max preferred distance for engagements (in meters).</summary>
        public float EngagementRange { get; set; } = 80f;

        /// <summary>Base aim precision rating (0.0 to 1.0).</summary>
        public float Accuracy { get; set; } = 0.7f;

        /// <summary>Priority for repositioning when exposed (0.0 to 1.0).</summary>
        public float RepositionPriority { get; set; } = 0.8f;

        /// <summary>Willingness to push or flank under uncertainty (0.0 to 1.0).</summary>
        public float RiskTolerance { get; set; } = 0.5f;

        /// <summary>Stickiness to teammates (0.0 solo → 1.0 squad cohesion).</summary>
        public float Cohesion { get; set; } = 0.75f;

        /// <summary>General hostility and combat bias (0.0 passive → 1.0 aggressive).</summary>
        public float AggressionLevel { get; set; } = 0.6f;

        #endregion

        #region Perception & Suppression Tuning

        /// <summary>Likelihood to investigate sounds or avoid ambushes.</summary>
        public float Caution { get; set; } = 0.5f;

        /// <summary>Probability to flinch or hesitate under fire.</summary>
        public float FlinchThreshold { get; set; } = 0.4f;

        /// <summary>Reaction sensitivity to suppression events.</summary>
        public float SuppressionSensitivity { get; set; } = 0.4f;

        #endregion

        #region Advanced Behavior Modulation

        /// <summary>Bias to take flanking paths during combat.</summary>
        public float FlankBias { get; set; } = 0.5f;

        /// <summary>Health-based fallback threshold (0.0 always fights → 1.0 retreats early).</summary>
        public float RetreatThreshold { get; set; } = 0.3f;

        /// <summary>How often bot uses suppressive fire instead of aiming directly.</summary>
        public float SuppressiveFireBias { get; set; } = 0.2f;

        /// <summary>How much this bot communicates with teammates (0.0 = silent).</summary>
        public float CommunicationLevel { get; set; } = 0.6f;

        /// <summary>Introduces unpredictability to timing and reactions.</summary>
        public float ChaosFactor { get; set; } = 0.0f;

        /// <summary>Accuracy penalty while under fire or panic.</summary>
        public float AccuracyUnderFire { get; set; } = 0.4f;

        #endregion

        #region Behavior Modifiers (Flags)

        /// <summary>Bot intentionally acts stupid or unpredictable.</summary>
        public bool IsDumb { get; set; } = false;

        /// <summary>Bot avoids all danger and retreats early.</summary>
        public bool IsFearful { get; set; } = false;

        /// <summary>Bot prefers static positions and rarely moves.</summary>
        public bool IsCamper { get; set; } = false;

        /// <summary>Bot ignores fear, flinching, and pushes aggressively.</summary>
        public bool IsFrenzied { get; set; } = false;

        /// <summary>Bot prefers ambush and low-audio profiles.</summary>
        public bool IsSilentHunter { get; set; } = false;

        /// <summary>Bot follows squad behavior strictly.</summary>
        public bool IsTeamPlayer { get; set; } = false;

        /// <summary>Bot exhibits cruel or aggressive emotional behavior.</summary>
        public bool IsSadistic { get; set; } = false;

        /// <summary>Bot refuses to retreat even in dangerous situations.</summary>
        public bool IsStubborn { get; set; } = false;

        #endregion
    }
}
