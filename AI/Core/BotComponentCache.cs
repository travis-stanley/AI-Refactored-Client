#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Behavior;
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// Caches all AIRefactored-related components and helpers for a bot.
    /// Used to unify AI behaviors, perception, and tactical modules into a single access point.
    /// </summary>
    public class BotComponentCache : MonoBehaviour
    {
        #region Core References

        /// <summary>
        /// The EFT BotOwner instance attached to this bot.
        /// </summary>
        public BotOwner? Bot { get; internal set; }

        public FlashGrenadeComponent? FlashGrenade { get; private set; }
        public BotPanicHandler? PanicHandler { get; private set; }
        public BotSuppressionReactionComponent? Suppression { get; private set; }
        public BotAIController? AIController { get; private set; }
        public AIRefactoredBotOwner? AIRefactoredBotOwner { get; private set; }
        public BotBehaviorEnhancer? BehaviorEnhancer { get; private set; }
        public BotOwnerPathfindingCache? PathCache { get; private set; }

        #endregion

        #region Perception State

        public bool IsBlinded { get; set; } = false;
        public float BlindUntilTime { get; set; } = 0f;
        public float LastFlashTime { get; set; } = 0f;

        public string? AssignedPersonalityName { get; set; }

        #endregion

        #region Hearing Tracking

        public float LastHeardTime { get; private set; } = -999f;
        public Vector3? LastHeardDirection { get; private set; }

        public void RegisterHeardSound(Vector3 source)
        {
            if (Bot?.GetPlayer == null || !Bot.GetPlayer.IsAI)
                return;

            LastHeardTime = Time.time;
            LastHeardDirection = source - Bot.Position;
        }

        #endregion

        #region Properties

        public bool IsReady =>
            Bot != null &&
            FlashGrenade != null &&
            PanicHandler != null &&
            Suppression != null &&
            AIController != null;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
            if (Bot == null)
            {
                Debug.LogError("[AIRefactored] BotComponentCache missing BotOwner component.");
                return;
            }

            FlashGrenade = GetComponent<FlashGrenadeComponent>();
            PanicHandler = GetComponent<BotPanicHandler>();
            Suppression = GetComponent<BotSuppressionReactionComponent>();
            AIController = GetComponent<BotAIController>();
            AIRefactoredBotOwner = GetComponent<AIRefactoredBotOwner>();
            BehaviorEnhancer = GetComponent<BotBehaviorEnhancer>();

            PathCache = new BotOwnerPathfindingCache();
        }

        #endregion
    }
}
