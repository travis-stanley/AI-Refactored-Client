#nullable enable

using EFT;
using UnityEngine;

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
        /// The live reference to this bot's BotOwner instance.
        /// </summary>
        public BotOwner? Bot { get; private set; }

        /// <summary>
        /// Cached reference to shared bot systems.
        /// </summary>
        public BotComponentCache? Cache { get; private set; }

        /// <summary>
        /// The current personality traits in use by this bot.
        /// </summary>
        public BotPersonalityProfile PersonalityProfile { get; private set; } = new BotPersonalityProfile();

        /// <summary>
        /// Name of the assigned personality, for logging or debugging.
        /// </summary>
        public string PersonalityName { get; private set; } = "Unknown";

        /// <summary>
        /// Name of the zone this bot was assigned to patrol or defend.
        /// </summary>
        public string AssignedZone { get; private set; } = "unknown";

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Attempts to auto-wire components and assign a default personality.
        /// </summary>
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
                InitProfile(GetRandomPersonality());
            }
        }

        #endregion

        #region Personality Management

        /// <summary>
        /// Initializes this bot's personality from a known preset type.
        /// </summary>
        public void InitProfile(PersonalityType type)
        {
            if (BotPersonalityPresets.Presets.TryGetValue(type, out BotPersonalityProfile? preset))
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
        /// Initializes this bot's personality from a raw profile object.
        /// </summary>
        public void InitProfile(BotPersonalityProfile profile, string name = "Custom")
        {
            PersonalityProfile = profile;
            PersonalityName = name;
        }

        /// <summary>
        /// Clears any currently assigned personality and resets to default.
        /// </summary>
        public void ClearPersonality()
        {
            PersonalityProfile = new BotPersonalityProfile();
            PersonalityName = "Cleared";
        }

        /// <summary>
        /// Returns whether this bot has a valid personality profile.
        /// </summary>
        public bool HasPersonality()
        {
            return PersonalityProfile != null;
        }

        #endregion

        #region Zone Assignment

        /// <summary>
        /// Assigns a named zone to this bot for tracking or patrol logic.
        /// </summary>
        public void SetZone(string zoneName)
        {
            if (!string.IsNullOrEmpty(zoneName))
                AssignedZone = zoneName;
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Picks a random PersonalityType enum value from known presets.
        /// </summary>
        private PersonalityType GetRandomPersonality()
        {
            System.Array values = System.Enum.GetValues(typeof(PersonalityType));
            return (PersonalityType)values.GetValue(Random.Range(0, values.Length));
        }

        #endregion
    }
}
