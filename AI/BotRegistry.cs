#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI
{
    /// <summary>
    /// Global runtime registry that links bot profile IDs to their assigned AIRefactored personality profiles.
    /// Enables lookup, dynamic registration, and fallback personality generation.
    /// </summary>
    public static class BotRegistry
    {
        #region Fields

        private static readonly Dictionary<string, BotPersonalityProfile> _registry = new(128);
        private static readonly HashSet<string> _missingLogged = new(64);
        private static bool _debug = true;

        #endregion

        #region Public API

        /// <summary>
        /// Registers a new personality profile for the given bot profile ID.
        /// </summary>
        public static void Register(string profileId, BotPersonalityProfile profile)
        {
            if (!_registry.ContainsKey(profileId))
            {
                _registry.Add(profileId, profile);

                if (_debug)
                    Debug.Log($"[AIRefactored:BotRegistry] Registered personality for bot '{profileId}': {profile.Personality}");
            }
        }

        /// <summary>
        /// Retrieves the registered personality profile for the specified bot.
        /// Falls back to Balanced if not found.
        /// </summary>
        public static BotPersonalityProfile Get(string profileId, PersonalityType fallback = PersonalityType.Balanced)
        {
            if (_registry.TryGetValue(profileId, out var profile))
                return profile;

            if (_debug && _missingLogged.Add(profileId))
                Debug.LogWarning($"[AIRefactored:BotRegistry] Missing personality for bot '{profileId}'. Defaulting to {fallback}.");

            return BotPersonalityPresets.GenerateProfile(fallback);
        }

        /// <summary>
        /// Tries to get a profile. Returns null if not found.
        /// </summary>
        public static BotPersonalityProfile? TryGet(string profileId)
        {
            if (_registry.TryGetValue(profileId, out var profile))
                return profile;

            return null;
        }

        /// <summary>
        /// Returns true if the specified bot profile ID has a registered personality profile.
        /// </summary>
        public static bool Exists(string profileId) => _registry.ContainsKey(profileId);

        /// <summary>
        /// Clears all registered profiles and warning logs.
        /// </summary>
        public static void Clear()
        {
            _registry.Clear();
            _missingLogged.Clear();

            if (_debug)
                Debug.Log("[AIRefactored:BotRegistry] Cleared all registered profiles and warnings.");
        }

        /// <summary>
        /// Enables or disables debug logging for registry actions at runtime.
        /// </summary>
        public static void EnableDebug(bool enable)
        {
            _debug = enable;
            Debug.Log($"[AIRefactored:BotRegistry] Debug logging {(enable ? "enabled" : "disabled")}.");
        }

        #endregion
    }
}
