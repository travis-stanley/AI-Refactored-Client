#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using System.Collections.Generic;

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
        private static ManualLogSource Logger => AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Registers a new personality profile for the given bot profile ID.
        /// </summary>
        /// <param name="profileId">Bot's unique profile ID string.</param>
        /// <param name="profile">Personality profile to associate with this bot.</param>
        public static void Register(string profileId, BotPersonalityProfile profile)
        {
            if (string.IsNullOrEmpty(profileId) || profile == null)
                return;

            if (!_registry.ContainsKey(profileId))
            {
                _registry.Add(profileId, profile);

                if (_debug)
                    Logger.LogInfo($"[BotRegistry] ✅ Registered profile for bot '{profileId}': {profile.Personality}");
            }
        }

        /// <summary>
        /// Retrieves the registered personality profile for the specified bot.
        /// If no profile is found, returns a generated fallback profile.
        /// </summary>
        /// <param name="profileId">Bot's unique profile ID string.</param>
        /// <param name="fallback">Fallback personality type to use if not registered.</param>
        /// <returns>The bot's assigned or fallback personality profile.</returns>
        public static BotPersonalityProfile Get(string profileId, PersonalityType fallback = PersonalityType.Balanced)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                if (_debug)
                    Logger.LogWarning("[BotRegistry] ⚠ Requested null or empty profileId. Returning default personality.");
                return BotPersonalityPresets.GenerateProfile(fallback);
            }

            if (_registry.TryGetValue(profileId, out var profile))
                return profile;

            if (_debug && _missingLogged.Add(profileId))
                Logger.LogWarning($"[BotRegistry] ❌ Missing personality for bot '{profileId}'. Defaulting to {fallback}.");

            return BotPersonalityPresets.GenerateProfile(fallback);
        }

        /// <summary>
        /// Tries to retrieve a personality profile for a bot. Returns null if not found.
        /// </summary>
        /// <param name="profileId">Bot profile ID to query.</param>
        /// <returns>The profile if found; otherwise null.</returns>
        public static BotPersonalityProfile? TryGet(string profileId)
        {
            if (string.IsNullOrEmpty(profileId))
                return null;

            return _registry.TryGetValue(profileId, out var profile) ? profile : null;
        }

        /// <summary>
        /// Returns true if the specified bot profile ID has a registered personality profile.
        /// </summary>
        /// <param name="profileId">Profile ID to check.</param>
        public static bool Exists(string profileId)
        {
            return !string.IsNullOrEmpty(profileId) && _registry.ContainsKey(profileId);
        }

        /// <summary>
        /// Clears all registered profiles and warning logs.
        /// </summary>
        public static void Clear()
        {
            _registry.Clear();
            _missingLogged.Clear();

            if (_debug)
                Logger.LogInfo("[BotRegistry] 🧹 Cleared all registered profiles and warnings.");
        }

        /// <summary>
        /// Enables or disables debug logging for registry actions at runtime.
        /// </summary>
        /// <param name="enable">True to enable, false to disable debug logs.</param>
        public static void EnableDebug(bool enable)
        {
            _debug = enable;
            Logger.LogInfo($"[BotRegistry] Debug logging {(enable ? "enabled" : "disabled")}.");
        }

        #endregion
    }
}
