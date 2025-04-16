#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.Data;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// Holds metadata and behavior profile for AIRefactored bots, including personality and zone logic.
    /// </summary>
    public class AIRefactoredBotOwner : MonoBehaviour
    {
        public BotOwner Bot { get; private set; } = null!;
        public BotComponentCache Cache { get; private set; } = null!;

        /// <summary>
        /// Assigned personality used to drive all tactical and psychological behavior.
        /// </summary>
        public BotPersonalityProfile PersonalityProfile { get; private set; } = new();

        /// <summary>
        /// Label used for debug and runtime inspection (e.g. Aggressive, Cautious, etc.).
        /// </summary>
        public string PersonalityName { get; private set; } = "Unknown";

        /// <summary>
        /// Zone name for fallback, group sync, or strategic positioning.
        /// </summary>
        public string AssignedZone { get; private set; } = "unknown";

        private void Awake()
        {
            Bot = GetComponent<BotOwner>()!;
            Cache = GetComponent<BotComponentCache>()!;

            if (Bot == null || Cache == null)
            {
                Debug.LogWarning("[AIRefactoredBotOwner] Missing BotOwner or BotComponentCache!");
                return;
            }

            // Assign a default personality if not explicitly set
            if (!HasPersonality() || PersonalityName == "Unknown")
            {
                var defaultPersonality = GetRandomPersonality();
                InitProfile(defaultPersonality);
            }
        }

        /// <summary>
        /// Initializes a preset personality by type.
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
                Debug.LogWarning($"[AIRefactoredBotOwner] Missing preset for {type}, defaulting to Adaptive.");
            }

#if UNITY_EDITOR
            Debug.Log($"[AIRefactoredBotOwner] Assigned personality '{PersonalityName}' to {Bot?.Profile?.Info?.Nickname}");
#endif
        }

        /// <summary>
        /// Initializes using a specific profile object.
        /// </summary>
        public void InitProfile(BotPersonalityProfile profile, string name = "Custom")
        {
            PersonalityProfile = profile ?? new BotPersonalityProfile();
            PersonalityName = name;

#if UNITY_EDITOR
            Debug.Log($"[AIRefactoredBotOwner] Assigned custom personality '{name}' to {Bot?.Profile?.Info?.Nickname}");
#endif
        }

        /// <summary>
        /// Clears the current personality and resets to defaults.
        /// </summary>
        public void ClearPersonality()
        {
            PersonalityProfile = new BotPersonalityProfile();
            PersonalityName = "Cleared";
        }

        /// <summary>
        /// Checks whether the bot has an assigned personality.
        /// </summary>
        public bool HasPersonality()
        {
            return PersonalityProfile != null;
        }

        /// <summary>
        /// Assigns the bot to a logical zone.
        /// </summary>
        public void SetZone(string zoneName)
        {
            if (!string.IsNullOrEmpty(zoneName))
            {
                AssignedZone = zoneName;
#if UNITY_EDITOR
                Debug.Log($"[AIRefactoredBotOwner] Bot {Bot?.Profile?.Info?.Nickname} assigned zone: {zoneName}");
#endif
            }
        }

        /// <summary>
        /// Picks a fallback personality at random from available presets.
        /// </summary>
        private PersonalityType GetRandomPersonality()
        {
            var values = System.Enum.GetValues(typeof(PersonalityType));
            return (PersonalityType)values.GetValue(Random.Range(0, values.Length));
        }
    }
}
