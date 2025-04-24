#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// Holds bot-specific personality, profile, and strategic coordination metadata.
    /// This wrapper is initialized during runtime and does not use MonoBehaviour lifecycle.
    /// </summary>
    public class AIRefactoredBotOwner
    {
        #region Logger

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Properties

        /// <summary>Main EFT BotOwner instance.</summary>
        public BotOwner? Bot { get; private set; }

        /// <summary>AIRefactored cache containing runtime references and helpers.</summary>
        public BotComponentCache? Cache { get; private set; }

        /// <summary>Behavioral profile for this bot (aggression, caution, etc).</summary>
        public BotPersonalityProfile PersonalityProfile { get; private set; } = new BotPersonalityProfile();

        /// <summary>Optional descriptive name for the personality.</summary>
        public string PersonalityName { get; private set; } = "Unknown";

        /// <summary>Assigned zone used for squad coordination or regional behaviors.</summary>
        public string AssignedZone { get; private set; } = "unknown";

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes this AIRefactored bot metadata wrapper.
        /// </summary>
        public void Initialize(BotOwner bot, BotComponentCache cache)
        {
            if (bot == null || cache == null)
                return;

            Bot = bot;
            Cache = cache;

            if (!HasPersonality())
                InitProfile(GetRandomPersonality());

            string name = bot.Profile?.Info?.Nickname ?? "Unknown";
            Logger.LogDebug($"[AIRefactored-Owner] Initialized for {name}");
        }

        #endregion

        #region Personality Setup

        /// <summary>
        /// Initializes from a predefined enum-based profile.
        /// </summary>
        public void InitProfile(PersonalityType type)
        {
            if (BotPersonalityPresets.Presets.TryGetValue(type, out var preset))
            {
                PersonalityProfile = preset;
                PersonalityName = type.ToString();
                Logger.LogInfo($"[AIRefactored-Owner] Personality '{PersonalityName}' assigned.");
            }
            else
            {
                PersonalityProfile = BotPersonalityPresets.Presets[PersonalityType.Adaptive];
                PersonalityName = "Adaptive";
                Logger.LogWarning($"[AIRefactored-Owner] Invalid preset '{type}', defaulted to Adaptive.");
            }
        }

        /// <summary>
        /// Initializes from a fully custom profile and name.
        /// </summary>
        public void InitProfile(BotPersonalityProfile profile, string name = "Custom")
        {
            PersonalityProfile = profile ?? new BotPersonalityProfile();
            PersonalityName = string.IsNullOrEmpty(name) ? "Custom" : name;
            Logger.LogInfo($"[AIRefactored-Owner] Custom personality '{PersonalityName}' assigned.");
        }

        /// <summary>
        /// Clears the active personality.
        /// </summary>
        public void ClearPersonality()
        {
            PersonalityProfile = new BotPersonalityProfile();
            PersonalityName = "Cleared";
            Logger.LogInfo("[AIRefactored-Owner] Personality cleared.");
        }

        /// <summary>
        /// True if a personality has been assigned to this bot.
        /// </summary>
        public bool HasPersonality()
        {
            return PersonalityProfile != null;
        }

        #endregion

        #region Zone Assignment

        /// <summary>
        /// Assigns a zone name for this bot (used for routing, fallback, etc).
        /// </summary>
        public void SetZone(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName))
            {
                Logger.LogWarning("[AIRefactored-Owner] Attempted to assign empty zone name.");
                return;
            }

            AssignedZone = zoneName;
            Logger.LogInfo($"[AIRefactored-Owner] Zone assigned: {zoneName}");
        }

        #endregion

        #region Internal Utilities

        /// <summary>
        /// Randomly selects a fallback profile from the enum (if not explicitly assigned).
        /// </summary>
        private PersonalityType GetRandomPersonality()
        {
            var values = (PersonalityType[])System.Enum.GetValues(typeof(PersonalityType));
            int roll = UnityEngine.Random.Range(0, values.Length);
            return values[roll];
        }

        #endregion
    }
}
