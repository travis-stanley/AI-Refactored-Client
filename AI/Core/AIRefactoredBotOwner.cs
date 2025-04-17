#nullable enable

using UnityEngine;
using EFT;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// Holds metadata and behavior profile for AIRefactored bots, including personality traits and zone logic.
    /// This component is used for high-level coordination and tuning of tactical behavior.
    /// </summary>
    public class AIRefactoredBotOwner : MonoBehaviour
    {
        #region Public Fields

        /// <summary>
        /// The underlying EFT BotOwner object.
        /// </summary>
        public BotOwner Bot { get; private set; } = null!;

        /// <summary>
        /// Cached internal components for performance and reuse.
        /// </summary>
        public BotComponentCache Cache { get; private set; } = null!;

        /// <summary>
        /// Assigned tactical and psychological profile.
        /// </summary>
        public BotPersonalityProfile PersonalityProfile { get; private set; } = new();

        /// <summary>
        /// Label used for personality debug tracking (e.g., Aggressive, Cautious).
        /// </summary>
        public string PersonalityName { get; private set; } = "Unknown";

        /// <summary>
        /// The zone this bot is assigned to for fallback, patrol, or coordination purposes.
        /// </summary>
        public string AssignedZone { get; private set; } = "unknown";

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Bot = GetComponent<BotOwner>()!;
            Cache = GetComponent<BotComponentCache>()!;

            if (Bot == null || Cache == null)
            {
                Debug.LogWarning("[AIRefactoredBotOwner] Missing BotOwner or BotComponentCache!");
                return;
            }

            // Assign default personality if unset
            if (!HasPersonality() || PersonalityName == "Unknown")
            {
                var defaultPersonality = GetRandomPersonality();
                InitProfile(defaultPersonality);
            }
        }

        #endregion

        #region Personality Management

        /// <summary>
        /// Initializes a preset personality from a known enum type.
        /// </summary>
        public void InitProfile(PersonalityType type)
        {
            if (BotPersonalityPresets.Presets.TryGetValue(type, out var preset))
            {
                PersonalityProfile = preset;
                PersonalityName = type.ToString();
            }
            else
            {
                PersonalityProfile = BotPersonalityPresets.Presets[PersonalityType.Adaptive];
                PersonalityName = "Adaptive";
                Debug.LogWarning($"[AIRefactoredBotOwner] Missing preset for {type}, defaulting to Adaptive.");
            }
        }

        /// <summary>
        /// Initializes personality directly using a custom profile object.
        /// </summary>
        public void InitProfile(BotPersonalityProfile profile, string name = "Custom")
        {
            PersonalityProfile = profile ?? new BotPersonalityProfile();
            PersonalityName = name;
        }

        /// <summary>
        /// Clears the assigned personality and resets profile to neutral/default.
        /// </summary>
        public void ClearPersonality()
        {
            PersonalityProfile = new BotPersonalityProfile();
            PersonalityName = "Cleared";
        }

        /// <summary>
        /// Returns true if this bot has a valid personality profile assigned.
        /// </summary>
        public bool HasPersonality()
        {
            return PersonalityProfile != null;
        }

        #endregion

        #region Zone Management

        /// <summary>
        /// Assigns a fallback or behavior logic zone to this bot.
        /// </summary>
        public void SetZone(string zoneName)
        {
            if (!string.IsNullOrEmpty(zoneName))
            {
                AssignedZone = zoneName;
            }
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Picks a random fallback personality from the known enum list.
        /// </summary>
        private PersonalityType GetRandomPersonality()
        {
            var values = System.Enum.GetValues(typeof(PersonalityType));
            return (PersonalityType)values.GetValue(Random.Range(0, values.Length));
        }

        #endregion
    }
}
