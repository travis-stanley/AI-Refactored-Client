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
        public BotOwner Bot { get; internal set; } = null!;

        /// <summary>
        /// Flash blindness detection and reaction module.
        /// </summary>
        public FlashGrenadeComponent? FlashGrenade { get; private set; }

        /// <summary>
        /// Panic behavior handler, triggers retreat or flinch behavior.
        /// </summary>
        public BotPanicHandler? PanicHandler { get; private set; }

        /// <summary>
        /// Suppression reaction handler for sprinting or cover-seeking.
        /// </summary>
        public BotSuppressionReactionComponent? Suppression { get; private set; }

        /// <summary>
        /// Main AI tick processor and routine controller.
        /// </summary>
        public BotAIController? AIController { get; private set; }

        /// <summary>
        /// Reference to the AIRefactoredBotOwner component holding metadata and profile.
        /// </summary>
        public AIRefactoredBotOwner? AIRefactoredBotOwner { get; private set; }

        /// <summary>
        /// Enhancer for post-engagement behavior such as loot or extract logic.
        /// </summary>
        public BotBehaviorEnhancer? BehaviorEnhancer { get; private set; }

        /// <summary>
        /// Local pathfinding and fallback caching system.
        /// </summary>
        public BotOwnerPathfindingCache? PathCache { get; private set; }

        #endregion

        #region Perception State

        /// <summary>
        /// Indicates whether bot is currently blinded.
        /// </summary>
        public bool IsBlinded { get; set; } = false;

        /// <summary>
        /// Time until blindness wears off.
        /// </summary>
        public float BlindUntilTime { get; set; } = 0f;

        /// <summary>
        /// Last time bot was flashed (for cooldown and behavioral impact).
        /// </summary>
        public float LastFlashTime { get; set; } = 0f;

        /// <summary>
        /// Assigned personality type label (used for debugging and grouping).
        /// </summary>
        public string? AssignedPersonalityName { get; set; }

        #endregion

        #region Hearing Tracking

        /// <summary>
        /// Last time the bot heard a valid sound cue.
        /// </summary>
        public float LastHeardTime { get; private set; } = -999f;

        /// <summary>
        /// Direction of last heard sound relative to bot position.
        /// </summary>
        public Vector3? LastHeardDirection { get; private set; }

        /// <summary>
        /// Registers a heard sound source and stores its relative direction and timestamp.
        /// Will not run for human players or Coop/FIKA players.
        /// </summary>
        /// <param name="source">World position of the heard sound.</param>
        public void RegisterHeardSound(Vector3 source)
        {
            if (Bot == null || Bot.GetPlayer == null || !Bot.GetPlayer.IsAI)
                return;

            LastHeardTime = Time.time;
            LastHeardDirection = source - Bot.Position;
        }

        #endregion

        #region Properties

        /// <summary>
        /// True if all essential AIRefactored components are present.
        /// </summary>
        public bool IsReady =>
            Bot != null &&
            FlashGrenade != null &&
            PanicHandler != null &&
            Suppression != null &&
            AIController != null;

        #endregion

        #region Unity Events

        /// <summary>
        /// Initializes all attached AIRefactored component references.
        /// </summary>
        private void Awake()
        {
            Bot = GetComponent<BotOwner>() ?? throw new MissingComponentException("Missing BotOwner on BotComponentCache");

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
