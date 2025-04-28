#nullable enable

namespace AIRefactored.AI
{
    using System.Diagnostics;

    /// <summary>
    ///     Defines a full personality configuration for AI bots, including behavior flags,
    ///     tactical parameters, suppression response, and mission preferences.
    /// </summary>
    [DebuggerDisplay("{Personality} Aggro={AggressionLevel}, Acc={Accuracy}, Chaos={ChaosFactor}")]
    public class BotPersonalityProfile
    {
        public float Accuracy { get; set; } = 0.7f;

        public float AccuracyUnderFire { get; set; } = 0.4f;

        public float AggressionLevel { get; set; } = 0.6f;

        public bool CanFlank => this.FlankBias > 0.05f;

        public bool CanRetreat => this.RetreatThreshold > 0.05f;

        public bool CanSuppress => this.SuppressiveFireBias > 0.05f;

        public float Caution { get; set; } = 0.5f;

        public float ChaosFactor { get; set; } = 0f;

        public float Cohesion { get; set; } = 0.75f;

        public float CommunicationLevel { get; set; } = 0.6f;

        public float CornerCheckPauseTime { get; set; } = 0.35f;

        public float EngagementRange { get; set; } = 80f;

        public float FlankBias { get; set; } = 0.5f;

        public float FlinchThreshold { get; set; } = 0.4f;

        public bool IsCamper { get; set; } = false;

        public bool IsDumb { get; set; } = false;

        public bool IsFearful { get; set; } = false;

        public bool IsFrenzied { get; set; } = false;

        public bool IsSadistic { get; set; } = false;

        public bool IsSilentHunter { get; set; } = false;

        public bool IsStubborn { get; set; } = false;

        public bool IsTeamPlayer { get; set; } = false;

        public LeanPreference LeaningStyle { get; set; } = LeanPreference.Conservative;

        public float LeanPeekFrequency { get; set; } = 0.5f;

        public float MovementJitter { get; set; } = 0.2f;

        public PersonalityType Personality { get; set; } = PersonalityType.Balanced;

        public MissionBias PreferredMission { get; set; } = MissionBias.Random;

        public float ReactionSpeed { get; set; } = 0.65f;

        public float ReactionTime { get; set; } = 0.25f;

        public float RepositionPriority { get; set; } = 0.8f;

        public float RetreatThreshold { get; set; } = 0.3f;

        public float RiskTolerance { get; set; } = 0.5f;

        public float SideStepBias { get; set; } = 0.5f;

        public float SuppressionSensitivity { get; set; } = 0.4f;

        public float SuppressiveFireBias { get; set; } = 0.2f;

        /// <summary>Creates a shallow clone of this profile.</summary>
        public BotPersonalityProfile Clone()
        {
            return (BotPersonalityProfile)this.MemberwiseClone();
        }

        public override string ToString()
        {
            return
                $"[{this.Personality}] Aggro={this.AggressionLevel}, Acc={this.Accuracy}, Chaos={this.ChaosFactor}, Cohesion={this.Cohesion}";
        }
    }

    #region Enums

    /// <summary>
    ///     Personality types define high-level AI behavior identity and bias.
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
    ///     Mission bias affects what the bot prefers to do on a match (loot, fight, quest).
    /// </summary>
    public enum MissionBias
    {
        Random,

        Loot,

        Fight,

        Quest
    }

    /// <summary>
    ///     Defines how often bots lean from cover during peeking or scanning.
    /// </summary>
    public enum LeanPreference
    {
        Never,

        Conservative,

        Aggressive
    }

    #endregion
}