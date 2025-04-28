#nullable enable

namespace AIRefactored.AI
{
    using System.Collections.Generic;

    using AIRefactored.AI.Core;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    /// <summary>
    /// Global personality registry that maps bot profile IDs to their assigned AIRefactored profiles.
    /// Supports registration, lookup, fallback generation, and debug diagnostics.
    /// </summary>
    public static class BotRegistry
    {
        private static readonly HashSet<string> _missingLogged = new(64);

        private static readonly Dictionary<string, AIRefactoredBotOwner> _ownerRegistry = new(128);

        private static readonly Dictionary<string, BotPersonalityProfile> _profileRegistry = new(128);

        private static bool _debug = true;

        private static ManualLogSource Logger => AIRefactoredController.Logger;

        /// <summary>
        /// Clears all registered profiles and owners.
        /// </summary>
        public static void Clear()
        {
            _profileRegistry.Clear();
            _ownerRegistry.Clear();
            _missingLogged.Clear();

            if (_debug)
                Logger.LogInfo("[BotRegistry] 🧹 Cleared all personality and owner data.");
        }

        /// <summary>
        /// Enables or disables debug logging.
        /// </summary>
        public static void EnableDebug(bool enable)
        {
            _debug = enable;
            Logger.LogInfo($"[BotRegistry] Debug logging {(enable ? "enabled" : "disabled")}.");
        }

        /// <summary>
        /// Checks if a profile is already registered.
        /// </summary>
        public static bool Exists(string profileId)
        {
            return !string.IsNullOrEmpty(profileId) && _profileRegistry.ContainsKey(profileId);
        }

        /// <summary>
        /// Retrieves a registered personality profile, or generates a fallback if missing.
        /// </summary>
        public static BotPersonalityProfile Get(string profileId, PersonalityType fallback = PersonalityType.Balanced)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                if (_debug)
                    Logger.LogWarning("[BotRegistry] Requested null/empty profileId. Returning fallback.");
                return BotPersonalityPresets.GenerateProfile(fallback);
            }

            if (_profileRegistry.TryGetValue(profileId, out var profile))
                return profile;

            if (_debug && _missingLogged.Add(profileId))
                Logger.LogWarning($"[BotRegistry] ❌ Missing profile for '{profileId}'. Using fallback: {fallback}.");

            return BotPersonalityPresets.GenerateProfile(fallback);
        }

        /// <summary>
        /// Registers a bot personality profile with the specified profile ID.
        /// </summary>
        public static void Register(string profileId, BotPersonalityProfile profile)
        {
            if (string.IsNullOrEmpty(profileId) || profile == null)
                return;

            if (_profileRegistry.ContainsKey(profileId))
                return;

            _profileRegistry[profileId] = profile;

            if (_debug)
                Logger.LogInfo($"[BotRegistry] ✅ Registered profile for '{profileId}': {profile.Personality}");
        }

        /// <summary>
        /// Registers a refactored bot owner for the specified profile ID.
        /// </summary>
        public static void RegisterOwner(string profileId, AIRefactoredBotOwner owner)
        {
            if (!string.IsNullOrEmpty(profileId) && owner != null)
                _ownerRegistry[profileId] = owner;
        }

        /// <summary>
        /// Tries to get a profile without generating a fallback. Returns null if not found.
        /// </summary>
        public static BotPersonalityProfile? TryGet(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return null;

            return _profileRegistry.TryGetValue(profileId, out var profile) ? profile : null;
        }

        /// <summary>
        /// Tries to retrieve a previously registered AIRefactoredBotOwner.
        /// </summary>
        public static AIRefactoredBotOwner? TryGetRefactoredOwner(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return null;

            return _ownerRegistry.TryGetValue(profileId, out var owner) ? owner : null;
        }
    }
}