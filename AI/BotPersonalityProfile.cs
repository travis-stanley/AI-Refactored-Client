#nullable enable

using System;
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

    #endregion

    /// <summary>
    /// Defines a full personality configuration for AI bots, including behavior flags,
    /// tactical parameters, suppression response, and mission preferences.
    /// </summary>
    [DebuggerDisplay("{Personality} Aggro={AggressionLevel}, Acc={Accuracy}, Chaos={ChaosFactor}")]
    public class BotPersonalityProfile
    {
        #region Identity

        /// <summary>
        /// Primary archetype defining this personality's behavioral baseline.
        /// </summary>
        public PersonalityType Personality { get; set; } = PersonalityType.Balanced;

        /// <summary>
        /// Preferred mission objective style for this personality.
        /// </summary>
        public MissionBias PreferredMission { get; set; } = MissionBias.Random;

        #endregion

        #region Core Tactical Behavior

        /// <summary>
        /// Preferred maximum combat engagement range (in meters).
        /// </summary>
        public float EngagementRange { get; set; } = 80f;

        /// <summary>
        /// Accuracy rating (0.0 = wildly inaccurate, 1.0 = precise).
        /// </summary>
        public float Accuracy { get; set; } = 0.7f;

        /// <summary>
        /// Likelihood to reposition or flank under exposure (0.0 = never, 1.0 = always).
        /// </summary>
        public float RepositionPriority { get; set; } = 0.8f;

        /// <summary>
        /// Willingness to risk encounters or push objectives (0.0 = extremely cautious, 1.0 = reckless).
        /// </summary>
        public float RiskTolerance { get; set; } = 0.5f;

        /// <summary>
        /// Loyalty to group and spacing behavior (0.0 = solo, 1.0 = squad cohesion).
        /// </summary>
        public float Cohesion { get; set; } = 0.75f;

        /// <summary>
        /// Tendency to initiate combat or push offensively (0.0 = passive, 1.0 = aggressive).
        /// </summary>
        public float AggressionLevel { get; set; } = 0.6f;

        #endregion

        #region Perception & Suppression Tuning

        /// <summary>
        /// Bot's caution level — affects sound investigation, cover usage, and peeking.
        /// </summary>
        public float Caution { get; set; } = 0.5f;

        /// <summary>
        /// Likelihood to flinch or hesitate when shot at (0.0 = fearless, 1.0 = very jumpy).
        /// </summary>
        public float FlinchThreshold { get; set; } = 0.4f;

        /// <summary>
        /// How easily bot enters suppressed state (0.0 = immune, 1.0 = very sensitive).
        /// </summary>
        public float SuppressionSensitivity { get; set; } = 0.4f;

        #endregion

        #region Advanced Behavior Modulation

        /// <summary>
        /// Bias toward taking alternate flanking paths in tactical movement.
        /// </summary>
        public float FlankBias { get; set; } = 0.5f;

        /// <summary>
        /// At what damage threshold the bot will fall back (0.0 = never retreats, 1.0 = retreats early).
        /// </summary>
        public float RetreatThreshold { get; set; } = 0.3f;

        /// <summary>
        /// Preference for suppressive fire over accurate shots.
        /// </summary>
        public float SuppressiveFireBias { get; set; } = 0.2f;

        /// <summary>
        /// Communication tendency with squad members (0.0 = silent, 1.0 = highly vocal).
        /// </summary>
        public float CommunicationLevel { get; set; } = 0.6f;

        /// <summary>
        /// Introduces random behavior and reaction delay (0.0 = predictable, 1.0 = chaotic).
        /// </summary>
        public float ChaosFactor { get; set; } = 0.0f;

        /// <summary>
        /// Penalty to bot accuracy when panicked or under fire.
        /// </summary>
        public float AccuracyUnderFire { get; set; } = 0.4f;

        #endregion

        #region Behavior Modifiers (Flags)

        /// <summary>
        /// Bot acts unintelligently and erratically (low coordination or decision-making).
        /// </summary>
        public bool IsDumb { get; set; } = false;

        /// <summary>
        /// Bot is fear-prone, prefers retreating and avoiding fights.
        /// </summary>
        public bool IsFearful { get; set; } = false;

        /// <summary>
        /// Bot prefers holding position and rarely advances.
        /// </summary>
        public bool IsCamper { get; set; } = false;

        /// <summary>
        /// Bot ignores fear, pushes enemies, and enters panic charge behavior.
        /// </summary>
        public bool IsFrenzied { get; set; } = false;

        /// <summary>
        /// Bot uses stealth, ambushes, and avoids detection.
        /// </summary>
        public bool IsSilentHunter { get; set; } = false;

        /// <summary>
        /// Bot strictly supports team objectives and communication.
        /// </summary>
        public bool IsTeamPlayer { get; set; } = false;

        /// <summary>
        /// Bot exhibits hostile emotional bias, such as cruelty or overkill.
        /// </summary>
        public bool IsSadistic { get; set; } = false;

        /// <summary>
        /// Bot will not retreat under any circumstance (high stubbornness).
        /// </summary>
        public bool IsStubborn { get; set; } = false;

        #endregion
    }
}
