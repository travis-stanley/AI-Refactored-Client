#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Zones;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Behavior;
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// Caches all AIRefactored AI components and helpers for a bot.
    /// Automatically attached and populated at bot initialization.
    /// </summary>
    public class BotComponentCache : MonoBehaviour
    {
        public BotOwner Bot { get; internal set; } = null!;

        public FlashGrenadeComponent? FlashGrenade { get; private set; }
        public BotPanicHandler? PanicHandler { get; private set; }
        public BotSuppressionReactionComponent? Suppression { get; private set; }
        public BotOwnerZone? Zone { get; private set; }
        public BotAIController? AIController { get; private set; }
        public AIRefactoredBotOwner? AIRefactoredBotOwner { get; private set; }
        public BotBehaviorEnhancer? BehaviorEnhancer { get; private set; }

        public bool IsBlinded { get; set; } = false;
        public float BlindUntilTime { get; set; } = 0f;
        public float LastFlashTime { get; set; } = 0f;
        public string? AssignedPersonalityName { get; set; }

        // === Hearing tracking ===
        public float LastHeardTime { get; private set; } = -999f;
        public Vector3? LastHeardDirection { get; private set; }

        /// <summary>
        /// Registers a heard sound and stores its time and direction.
        /// </summary>
        public void RegisterHeardSound(Vector3 source)
        {
            LastHeardTime = Time.time;
            LastHeardDirection = source - Bot.Position;
        }

        public bool IsReady =>
            Bot != null &&
            FlashGrenade != null &&
            PanicHandler != null &&
            Suppression != null &&
            AIController != null;

        private void Awake()
        {
            Bot = GetComponent<BotOwner>() ?? throw new MissingComponentException("Missing BotOwner on BotComponentCache");

            FlashGrenade = GetComponent<FlashGrenadeComponent>();
            PanicHandler = GetComponent<BotPanicHandler>();
            Suppression = GetComponent<BotSuppressionReactionComponent>();
            Zone = GetComponent<BotOwnerZone>();
            AIController = GetComponent<BotAIController>();
            AIRefactoredBotOwner = GetComponent<AIRefactoredBotOwner>();
            BehaviorEnhancer = GetComponent<BotBehaviorEnhancer>();

#if UNITY_EDITOR
            if (!IsReady)
            {
                Debug.LogWarning($"[AIRefactored] BotComponentCache on {Bot.Profile?.Info?.Nickname ?? "?"} is missing required components");
            }
#endif
        }
    }
}
