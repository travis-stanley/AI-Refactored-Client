#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using System.Collections.Generic;

namespace AIRefactored.AI
{
    /// <summary>
    /// Global personality registry that maps bot profile IDs to their assigned AIRefactored profiles.
    /// Supports registration, lookup, fallback generation, and debug diagnostics.
    /// </summary>
    public static class BotRegistry
    {
        #region Internal Storage

        private static readonly Dictionary<string, BotPersonalityProfile> _registry = new(128);
        private static readonly HashSet<string> _missingLogged = new(64);
        private static bool _debug = true;

        private static ManualLogSource Logger => AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Registers a bot personality profile with the specified profile ID.
        /// </summary>
        public static void Register(string profileId, BotPersonalityProfile profile)
        {
            if (string.IsNullOrEmpty(profileId) || profile == null)
                return;

            if (_registry.ContainsKey(profileId))
                return;

            _registry.Add(profileId, profile);

            if (_debug)
                Logger.LogInfo($"[BotRegistry] ✅ Registered profile for '{profileId}': {profile.Personality}");
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

            if (_registry.TryGetValue(profileId, out var profile))
                return profile;

            if (_debug && _missingLogged.Add(profileId))
                Logger.LogWarning($"[BotRegistry] ❌ Missing profile for '{profileId}'. Using fallback: {fallback}.");

            return BotPersonalityPresets.GenerateProfile(fallback);
        }

        /// <summary>
        /// Tries to get a profile without generating a fallback. Returns null if not found.
        /// </summary>
        public static BotPersonalityProfile? TryGet(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return null;

            return _registry.TryGetValue(profileId, out var profile) ? profile : null;
        }

        /// <summary>
        /// Checks if a profile is already registered.
        /// </summary>
        public static bool Exists(string profileId)
        {
            return !string.IsNullOrEmpty(profileId) && _registry.ContainsKey(profileId);
        }

        /// <summary>
        /// Clears all registered profiles and missing logs.
        /// </summary>
        public static void Clear()
        {
            _registry.Clear();
            _missingLogged.Clear();

            if (_debug)
                Logger.LogInfo("[BotRegistry] 🧹 Cleared registry and missing-profile log.");
        }

        /// <summary>
        /// Enables or disables debug logging for this system.
        /// </summary>
        public static void EnableDebug(bool enable)
        {
            _debug = enable;
            Logger.LogInfo($"[BotRegistry] Debug logging {(enable ? "enabled" : "disabled")}.");
        }

        #endregion
    }
}
