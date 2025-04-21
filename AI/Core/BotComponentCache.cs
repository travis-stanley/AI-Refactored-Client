#nullable enable

using AIRefactored.AI.Behavior;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Components;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Medical;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Movement;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Reactions;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// Central cache and coordinator for all AIRefactored logic on a bot.
    /// Holds references to combat, movement, perception, and tactical modules.
    /// </summary>
    public class BotComponentCache
    {
        #region Core Bot Reference

        /// <summary>
        /// The primary BotOwner reference for this AI bot.
        /// </summary>
        public BotOwner? Bot { get; internal set; }

        #endregion

        #region AI Subsystems

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

        public SquadPathCoordinator? SquadPath { get; private set; }
        public BotTacticalMemory? TacticalMemory { get; private set; }

        #endregion

        #region Tactical Modules

        /// <summary>Target selector (updated each tick).</summary>
        public BotThreatSelector ThreatSelector = null!;
        /// <summary>Tracks bot injuries and healing needs.</summary>
        public BotInjurySystem InjurySystem = null!;
        /// <summary>Tracks last attackers and outgoing fire memory.</summary>
        public BotLastShotTracker LastShotTracker = null!;
        /// <summary>Handles group comms like fallback echos and spotted calls.</summary>
        public BotGroupComms GroupComms = null!;

        #endregion

        #region Perception Flags

        /// <summary>True if the bot is currently blinded.</summary>
        public bool IsBlinded { get; set; }

        /// <summary>Time until bot vision recovers from flashbang.</summary>
        public float BlindUntilTime { get; set; }

        /// <summary>Timestamp of last flash exposure.</summary>
        public float LastFlashTime { get; set; }

        /// <summary>Optional real-time visibility data shared with vision/perception systems.</summary>
        public TrackedEnemyVisibility? VisibilityTracker;

        #endregion

        #region Hearing Context

        /// <summary>Last time the bot heard a sound.</summary>
        public float LastHeardTime { get; private set; } = -999f;

        /// <summary>Direction the last heard sound came from.</summary>
        public Vector3? LastHeardDirection { get; private set; }

        /// <summary>
        /// Registers a sound heard by the bot. Updates direction and timestamp.
        /// </summary>
        /// <param name="source">World-space position the sound originated from.</param>
        public void RegisterHeardSound(Vector3 source)
        {
            if (Bot == null || Bot.GetPlayer == null || !Bot.GetPlayer.IsAI)
                return;

            LastHeardTime = Time.time;
            LastHeardDirection = source - Bot.Position;
        }

        #endregion

        #region Shortcuts & Properties

        /// <summary>
        /// Current bot world position (fallbacks to zero if bot null).
        /// </summary>
        public Vector3 Position => Bot?.Position ?? Vector3.zero;

        /// <summary>
        /// Memory access helper for this bot.
        /// </summary>
        public BotMemoryClass? Memory => Bot?.Memory;

        /// <summary>
        /// Shortcut alias for accessing panic handler (legacy).
        /// </summary>
        public BotPanicHandler? Panic => PanicHandler;

        /// <summary>
        /// Whether all core AIRefactored components are initialized.
        /// </summary>
        public bool IsReady =>
            Bot != null &&
            FlashGrenade != null &&
            PanicHandler != null &&
            Suppression != null &&
            Movement != null &&
            Tactical != null;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes all AI subsystems and memory caches for this bot.
        /// </summary>
        /// <param name="bot">EFT BotOwner instance.</param>
        public void Initialize(BotOwner bot)
        {
            Bot = bot;

            FlashGrenade = new FlashGrenadeComponent();
            FlashGrenade.Initialize(this);

            PanicHandler = new BotPanicHandler();
            PanicHandler.Initialize(this);

            Suppression = new BotSuppressionReactionComponent();
            Suppression.Initialize(this);

            BehaviorEnhancer = new BotBehaviorEnhancer();
            BehaviorEnhancer.Initialize(this);

            GroupBehavior = new BotGroupBehavior();
            GroupBehavior.Initialize(this);

            Movement = new BotMovementController();
            Movement.Initialize(this);

            Tactical = new BotTacticalDeviceController();
            Tactical.Initialize(bot, this);

            HearingDamage = new HearingDamageComponent();

            Combat = new CombatStateMachine();
            Combat.Initialize(this);

            Tilt = new BotTilt(bot);
            PathCache = new BotOwnerPathfindingCache();

            SquadPath = new SquadPathCoordinator();
            SquadPath.Initialize(this);

            TacticalMemory = new BotTacticalMemory();
            TacticalMemory.Initialize(this);

            AIRefactoredBotOwner = bot.GetPlayer?.GetComponent<AIRefactoredBotOwner>();

            ThreatSelector = new BotThreatSelector(this);
            InjurySystem = new BotInjurySystem(this);
            LastShotTracker = new BotLastShotTracker();
            GroupComms = new BotGroupComms(this);
        }

        #endregion

        #region Reset & Clear

        /// <summary>
        /// Clears volatile runtime flags and perception markers.
        /// Should be called on bot respawn or major state reset.
        /// </summary>
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
