#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// AIRefactored behavior wrapper that manages bot metadata and profile initialization.
    /// Tracks personality, assigned zones, and coordination with BotComponentCache.
    /// </summary>
    public class AIRefactoredBotOwner : MonoBehaviour
    {
        #region Public Properties

        /// <summary>
        /// The underlying EFT BotOwner component.
        /// </summary>
        public BotOwner? Bot { get; private set; }

        /// <summary>
        /// Cached bot component references (brain, AI logic, etc.).
        /// </summary>
        public BotComponentCache? Cache { get; private set; }

        /// <summary>
        /// The currently assigned bot personality profile.
        /// </summary>
        public BotPersonalityProfile PersonalityProfile { get; private set; } = new BotPersonalityProfile();

        /// <summary>
        /// Human-readable name of the personality type.
        /// </summary>
        public string PersonalityName { get; private set; } = "Unknown";

        /// <summary>
        /// Optional name of the strategic zone this bot is assigned to.
        /// </summary>
        public string AssignedZone { get; private set; } = "unknown";

        #endregion

        #region Fields

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
            Cache = GetComponent<BotComponentCache>();

            if (Bot == null || Cache == null)
            {
                Logger.LogWarning("[AIRefactored-Owner] ❌ Missing BotOwner or BotComponentCache.");
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
        /// Initializes the bot with a predefined personality preset.
        /// </summary>
        /// <param name="type">The preset type to assign.</param>
        public void InitProfile(PersonalityType type)
        {
            if (BotPersonalityPresets.Presets.TryGetValue(type, out var preset))
            {
                PersonalityProfile = preset;
                PersonalityName = type.ToString();
                Logger.LogInfo($"[AIRefactored-Owner] Personality {PersonalityName} assigned.");
            }
            else
            {
                PersonalityProfile = BotPersonalityPresets.Presets[PersonalityType.Adaptive];
                PersonalityName = "Adaptive";
                Logger.LogWarning($"[AIRefactored-Owner] Unknown preset {type}, defaulting to Adaptive.");
            }
        }

        /// <summary>
        /// Assigns a fully customized personality profile.
        /// </summary>
        /// <param name="profile">The profile to assign.</param>
        /// <param name="name">Optional display name.</param>
        public void InitProfile(BotPersonalityProfile profile, string name = "Custom")
        {
            PersonalityProfile = profile ?? new BotPersonalityProfile();
            PersonalityName = name;
            Logger.LogInfo($"[AIRefactored-Owner] Custom personality {PersonalityName} assigned.");
        }

        /// <summary>
        /// Resets the bot's personality to default.
        /// </summary>
        public void ClearPersonality()
        {
            PersonalityProfile = new BotPersonalityProfile();
            PersonalityName = "Cleared";
            Logger.LogInfo("[AIRefactored-Owner] Personality cleared.");
        }

        /// <summary>
        /// Checks if a personality has been initialized.
        /// </summary>
        public bool HasPersonality()
        {
            return PersonalityProfile != null;
        }

        #endregion

        #region Zone Assignment

        /// <summary>
        /// Assigns the bot to a named tactical zone (used in routing, squads, etc.).
        /// </summary>
        /// <param name="zoneName">Name of the zone to assign.</param>
        public void SetZone(string zoneName)
        {
            if (!string.IsNullOrEmpty(zoneName))
            {
                AssignedZone = zoneName;
                Logger.LogInfo($"[AIRefactored-Owner] Bot assigned to zone: {zoneName}");
            }
            else
            {
                Logger.LogWarning("[AIRefactored-Owner] Attempted to assign empty zone name.");
            }
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Picks a random personality preset from available enum values.
        /// </summary>
        private PersonalityType GetRandomPersonality()
        {
            var values = System.Enum.GetValues(typeof(PersonalityType));
            return (PersonalityType)values.GetValue(Random.Range(0, values.Length));
        }

        #endregion
    }
}
