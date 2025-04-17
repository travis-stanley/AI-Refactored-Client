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

        /// <summary> Handles flashbang effects and blind logic. </summary>
        public FlashGrenadeComponent? FlashGrenade { get; private set; }

        /// <summary> Controls panic behavior and retreat triggers. </summary>
        public BotPanicHandler? PanicHandler { get; private set; }

        /// <summary> Handles suppression sprint/retreat behavior. </summary>
        public BotSuppressionReactionComponent? Suppression { get; private set; }

        /// <summary> Central tick and routine processor. </summary>
        public BotAIController? AIController { get; private set; }

        /// <summary> High-level owner wrapper for personality and group logic. </summary>
        public AIRefactoredBotOwner? AIRefactoredBotOwner { get; private set; }

        /// <summary> Handles post-combat logic like looting and extracting. </summary>
        public BotBehaviorEnhancer? BehaviorEnhancer { get; private set; }

        /// <summary> Pathfinding cache for cover, fallback, and group offsets. </summary>
        public BotOwnerPathfindingCache? PathCache { get; private set; }

        #endregion

        #region Perception State

        public bool IsBlinded { get; set; } = false;
        public float BlindUntilTime { get; set; } = 0f;
        public float LastFlashTime { get; set; } = 0f;

        #endregion

        #region Hearing Tracking

        public float LastHeardTime { get; private set; } = -999f;
        public Vector3? LastHeardDirection { get; private set; }

        /// <summary>
        /// Registers a sound event heard by this bot.
        /// </summary>
        /// <param name="source">The world position of the sound.</param>
        public void RegisterHeardSound(Vector3 source)
        {
            if (Bot?.GetPlayer == null || !Bot.GetPlayer.IsAI)
                return;

            LastHeardTime = Time.time;
            LastHeardDirection = source - Bot.Position;
        }

        #endregion

        #region Properties

        /// <summary>
        /// True if all essential core AIRefactored modules are present on this bot.
        /// </summary>
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
                Debug.LogError("[AIRefactored-Cache] ❌ BotComponentCache missing BotOwner!");
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
