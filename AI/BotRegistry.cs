#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI
{
    /// <summary>
    /// Global runtime registry that links bot profile IDs to assigned AIRefactored personality profiles.
    /// Enables retrieval, registration, and fallback behavior for all active bots.
    /// </summary>
    public static class BotRegistry
    {
        #region Fields

        private static readonly Dictionary<string, BotPersonalityProfile> _registry = new();

        // Optional: to avoid log spam on fallback
        private static readonly HashSet<string> _missingLogged = new();

        // Optional toggle
        private static bool _debug = true;

        #endregion

        #region Public API

        /// <summary>
        /// Registers a new personality profile for a bot.
        /// </summary>
        public static void Register(string profileId, BotPersonalityProfile profile)
        {
            if (!_registry.ContainsKey(profileId))
            {
                _registry.Add(profileId, profile);
                if (_debug)
                    Debug.Log($"[AIRefactored] Registered personality for bot {profileId}: {profile.Personality}");
            }
        }

        /// <summary>
        /// Retrieves the personality profile for a bot. Falls back to Balanced if not registered.
        /// </summary>
        public static BotPersonalityProfile Get(string profileId)
        {
            if (_registry.TryGetValue(profileId, out var profile))
                return profile;

            if (_debug && !_missingLogged.Contains(profileId))
            {
                Debug.LogWarning($"[AIRefactored] Missing personality for bot {profileId}. Defaulting to Balanced.");
                _missingLogged.Add(profileId);
            }

            return BotPersonalityPresets.GenerateProfile(PersonalityType.Balanced);
        }

        /// <summary>
        /// Checks whether a bot has an assigned personality.
        /// </summary>
        public static bool Exists(string profileId) => _registry.ContainsKey(profileId);

        /// <summary>
        /// Clears all registered profiles and missing logs.
        /// </summary>
        public static void Clear()
        {
            _registry.Clear();
            _missingLogged.Clear();
            Debug.Log("[AIRefactored] Bot personality registry cleared.");
        }

        /// <summary>
        /// Enables or disables debug logging at runtime.
        /// </summary>
        public static void EnableDebug(bool enable)
        {
            _debug = enable;
        }

        #endregion
    }
}
