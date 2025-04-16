#nullable enable
using System.Diagnostics;

namespace AIRefactored.AI
{
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

    /// <summary>
    /// AI personality structure used throughout AIRefactored. Each field can be tuned per wave, per bot, or generated from presets.
    /// </summary>
    [DebuggerDisplay("{Personality} Aggro={AggressionLevel}, Acc={Accuracy}, Chaos={ChaosFactor}")]
    public class BotPersonalityProfile
    {
        /// <summary>Primary personality archetype label.</summary>
        public PersonalityType Personality { get; set; } = PersonalityType.Balanced;

        /// <summary>Optional bias toward a mission type (loot, fight, quest, or random).</summary>
        public MissionBias PreferredMission { get; set; } = MissionBias.Random;

        // Core tactical behavior
        public float EngagementRange { get; set; } = 80f;
        public float Accuracy { get; set; } = 0.7f;
        public float RepositionPriority { get; set; } = 0.8f;
        public float RiskTolerance { get; set; } = 0.5f;
        public float Cohesion { get; set; } = 0.75f;
        public float AggressionLevel { get; set; } = 0.6f;

        // AI perception & suppression tuning
        public float Caution { get; set; } = 0.5f;
        public float FlinchThreshold { get; set; } = 0.4f;
        public float SuppressionSensitivity { get; set; } = 0.4f;

        // Advanced behavior modulation
        public float FlankBias { get; set; } = 0.5f;
        public float RetreatThreshold { get; set; } = 0.3f;
        public float SuppressiveFireBias { get; set; } = 0.2f;
        public float CommunicationLevel { get; set; } = 0.6f;
        public float ChaosFactor { get; set; } = 0.0f;
        public float AccuracyUnderFire { get; set; } = 0.4f;

        // Behavior modifiers
        public bool IsDumb { get; set; } = false;
        public bool IsFearful { get; set; } = false;
        public bool IsCamper { get; set; } = false;
        public bool IsFrenzied { get; set; } = false;
        public bool IsSilentHunter { get; set; } = false;
        public bool IsTeamPlayer { get; set; } = false;
        public bool IsSadistic { get; set; } = false;
        public bool IsStubborn { get; set; } = false;
    }
}
