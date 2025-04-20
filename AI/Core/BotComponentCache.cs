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
    /// Caches all AIRefactored-related components and helpers for a bot.
    /// Used to unify AI behaviors, perception, and tactical modules into a single access point.
    /// </summary>
    public class BotComponentCache
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

        public SquadPathCoordinator? SquadPath { get; private set; }
        public BotTacticalMemory? TacticalMemory { get; private set; }

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

        public Vector3 Position => Bot?.Position ?? Vector3.zero;

        public BotMemoryClass? Memory => Bot?.Memory;

        public BotPanicHandler? Panic => PanicHandler;

        #endregion

        #region Visibility Tracking

        public TrackedEnemyVisibility? VisibilityTracker;

        #endregion

        #region Initialization

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

            AIRefactoredBotOwner = null;

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
