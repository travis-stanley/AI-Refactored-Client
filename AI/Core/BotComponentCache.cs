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

        public BotOwner Bot { get; internal set; } = null!;

        public FlashGrenadeComponent? FlashGrenade { get; private set; }
        public BotPanicHandler? PanicHandler { get; private set; }
        public BotSuppressionReactionComponent? Suppression { get; private set; }
        public BotOwnerZone? Zone { get; private set; }
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

        /// <summary>
        /// Registers a heard sound source and stores its relative direction and timestamp.
        /// Will not run for human players or Coop/FIKA players.
        /// </summary>
        public void RegisterHeardSound(Vector3 source)
        {
            if (Bot == null || Bot.GetPlayer == null || !Bot.GetPlayer.IsAI)
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

        #region Unity Events

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

            PathCache = new BotOwnerPathfindingCache();
        }

        #endregion
    }
}
