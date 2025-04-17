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
        public BotOwner? Bot { get; private set; }

        /// <summary>
        /// Cached internal components for performance and reuse.
        /// </summary>
        public BotComponentCache? Cache { get; private set; }

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
            Bot = GetComponent<BotOwner>();
            Cache = GetComponent<BotComponentCache>();

            if (Bot == null || Cache == null)
            {
                Debug.LogWarning("[AIRefactoredBotOwner] Missing BotOwner or BotComponentCache!");
                return;
            }

            if (!HasPersonality() || PersonalityName == "Unknown")
            {
                var defaultPersonality = GetRandomPersonality();
                InitProfile(defaultPersonality);
            }
        }

        #endregion

        #region Personality Management

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

        public void InitProfile(BotPersonalityProfile profile, string name = "Custom")
        {
            PersonalityProfile = profile ?? new BotPersonalityProfile();
            PersonalityName = name;
        }

        public void ClearPersonality()
        {
            PersonalityProfile = new BotPersonalityProfile();
            PersonalityName = "Cleared";
        }

        public bool HasPersonality()
        {
            return PersonalityProfile != null && PersonalityName != "Unknown";
        }

        #endregion

        #region Zone Management

        public void SetZone(string zoneName)
        {
            if (!string.IsNullOrEmpty(zoneName))
            {
                AssignedZone = zoneName;
            }
        }

        #endregion

        #region Internal Helpers

        private PersonalityType GetRandomPersonality()
        {
            var values = System.Enum.GetValues(typeof(PersonalityType));
            return (PersonalityType)values.GetValue(Random.Range(0, values.Length));
        }

        #endregion
    }
}
