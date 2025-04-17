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
        #region Public Properties

        /// <summary>
        /// The underlying EFT BotOwner instance.
        /// </summary>
        public BotOwner? Bot { get; private set; }

        /// <summary>
        /// Cached component access for AI subsystems.
        /// </summary>
        public BotComponentCache? Cache { get; private set; }

        /// <summary>
        /// Current personality profile defining behavior.
        /// </summary>
        public BotPersonalityProfile PersonalityProfile { get; private set; } = new();

        /// <summary>
        /// Readable label for debugging or display (e.g. Aggressive, Cautious).
        /// </summary>
        public string PersonalityName { get; private set; } = "Unknown";

        /// <summary>
        /// Tactical zone name used for fallback or patrol logic.
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
                Debug.LogWarning("[AIRefactored-Owner] ❌ Missing BotOwner or BotComponentCache.");
                return;
            }

            if (!HasPersonality())
            {
                var defaultType = GetRandomPersonality();
                InitProfile(defaultType);
            }
        }

        #endregion

        #region Personality Management

        /// <summary>
        /// Assigns a named preset profile to the bot.
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
                Debug.LogWarning($"[AIRefactored-Owner] Unknown preset {type}, defaulting to Adaptive.");
            }
        }

        /// <summary>
        /// Assigns a custom personality object with optional label.
        /// </summary>
        public void InitProfile(BotPersonalityProfile profile, string name = "Custom")
        {
            PersonalityProfile = profile;
            PersonalityName = name;
        }

        /// <summary>
        /// Clears the assigned personality and resets to neutral.
        /// </summary>
        public void ClearPersonality()
        {
            PersonalityProfile = new BotPersonalityProfile();
            PersonalityName = "Cleared";
        }

        /// <summary>
        /// Returns true if a personality has been assigned.
        /// </summary>
        public bool HasPersonality()
        {
            return PersonalityProfile != null;
        }

        #endregion

        #region Zone Assignment

        /// <summary>
        /// Sets the tactical zone ID for fallback, patrol, or formation logic.
        /// </summary>
        public void SetZone(string zoneName)
        {
            if (!string.IsNullOrEmpty(zoneName))
                AssignedZone = zoneName;
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
