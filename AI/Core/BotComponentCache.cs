#nullable enable

using AIRefactored.AI.Behavior;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Components;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Medical;
using AIRefactored.AI.Movement;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Reactions;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// Caches all AIRefactored-related components and helpers for a bot.
    /// Used to unify AI behaviors, perception, and tactical modules into a single access point.
    /// </summary>
    public class BotComponentCache : MonoBehaviour
    {
        #region Core References

        public BotOwner? Bot { get; internal set; }

        public FlashGrenadeComponent? FlashGrenade { get; private set; }
        public BotPanicHandler? PanicHandler { get; private set; }
        public BotSuppressionReactionComponent? Suppression { get; private set; }

        public AIRefactoredBotOwner? AIRefactoredBotOwner { get; private set; }
        public BotBehaviorEnhancer? BehaviorEnhancer { get; private set; }
        public BotGroupBehavior? GroupBehavior { get; private set; }

        public BotMovementController? Movement { get; private set; }
        public BotTacticalDeviceController? Tactical { get; private set; }

        public HearingDamageComponent? HearingDamage { get; private set; }
        public CombatStateMachine? Combat { get; private set; }

        public BotOwnerPathfindingCache? PathCache { get; private set; }
        public BotTilt? Tilt { get; private set; }

        public BotPoseController? PoseController { get; set; }

        #endregion

        #region Tactical Modules

        public BotThreatSelector ThreatSelector = null!;
        public BotInjurySystem InjurySystem = null!;
        public BotLastShotTracker LastShotTracker = null!;
        public BotGroupComms GroupComms = null!;

        #endregion

        #region Perception State

        public bool IsBlinded { get; set; } = false;
        public float BlindUntilTime { get; set; } = 0f;
        public float LastFlashTime { get; set; } = 0f;

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
            Movement != null &&
            Tactical != null;

        #endregion

        #region Shortcuts

        /// <summary>
        /// Returns the bot’s current world position or fallback to transform if unset.
        /// </summary>
        public Vector3 Position => Bot?.Position ?? transform.position;

        /// <summary>
        /// Shortcut to BotOwner.Memory.
        /// </summary>
        public BotMemoryClass? Memory => Bot?.Memory;

        /// <summary>
        /// Alias for PanicHandler.
        /// </summary>
        public BotPanicHandler? Panic => PanicHandler;

        #endregion

        #region Visibility Tracking

        /// <summary>
        /// Tracks partial enemy visibility (head/torso) for accurate suppression/fire logic.
        /// </summary>
        public TrackedEnemyVisibility? VisibilityTracker;

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
            AIRefactoredBotOwner = GetComponent<AIRefactoredBotOwner>();
            BehaviorEnhancer = GetComponent<BotBehaviorEnhancer>();
            GroupBehavior = GetComponent<BotGroupBehavior>();
            Movement = GetComponent<BotMovementController>();
            Tactical = GetComponent<BotTacticalDeviceController>();
            HearingDamage = GetComponent<HearingDamageComponent>();
            Combat = GetComponent<CombatStateMachine>();

            PathCache = new BotOwnerPathfindingCache();
            Tilt = Bot != null ? new BotTilt(Bot) : null;

            // Tactical modules
            ThreatSelector = new BotThreatSelector(this);
            InjurySystem = new BotInjurySystem(this);
            LastShotTracker = new BotLastShotTracker();
            GroupComms = new BotGroupComms(this);
        }

        #endregion

        #region Reset Support

        public void Reset()
        {
            IsBlinded = false;
            BlindUntilTime = 0f;
            LastFlashTime = 0f;
            LastHeardTime = -999f;
            LastHeardDirection = null;
            VisibilityTracker = null;

            PathCache?.Clear();
        }

        #endregion
    }
}
