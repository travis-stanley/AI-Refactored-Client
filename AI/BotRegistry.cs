using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI
{
    public static class BotRegistry
    {
        private static readonly Dictionary<string, BotPersonalityProfile> _registry = new();

        public static void Register(string profileId, BotPersonalityProfile profile)
        {
            if (!_registry.ContainsKey(profileId))
            {
                _registry.Add(profileId, profile);
                Debug.Log($"[AIRefactored] Registered personality for bot {profileId}: {profile.Personality}");
            }
        }

        public static BotPersonalityProfile Get(string profileId)
        {
            if (_registry.TryGetValue(profileId, out var profile))
                return profile;

            Debug.LogWarning($"[AIRefactored] Missing personality for bot {profileId}. Defaulting to Balanced.");
            return BotPersonalityPresets.GenerateProfile(PersonalityType.Balanced);
        }

        public static bool Exists(string profileId)
        {
            return _registry.ContainsKey(profileId);
        }

        public static void Clear()
        {
            Debug.Log("[AIRefactored] Bot personality registry cleared.");
            _registry.Clear();
        }
    }
}