#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Components;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Looting;
using AIRefactored.AI.Medical;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Movement;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Reactions;
using AIRefactored.Runtime;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using System;
using UnityEngine;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// Central runtime cache of all AIRefactored subsystems for an individual bot.
    /// Used to streamline access, avoid reflection, and coordinate inter-system updates.
    /// </summary>
    public sealed class BotComponentCache
    {
        #region Logger

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Core Identity

        public BotOwner? Bot { get; internal set; }
        public AIRefactoredBotOwner? AIRefactoredBotOwner { get; private set; }

        #endregion

        #region AI Systems

        public BotThreatEscalationMonitor? Escalation { get; private set; }
        public FlashGrenadeComponent? FlashGrenade { get; private set; }
        public BotPanicHandler? PanicHandler { get; private set; }
        public BotSuppressionReactionComponent? Suppression { get; private set; }
        public BotGroupBehavior? GroupBehavior { get; private set; }
        public BotMovementController? Movement { get; private set; }
        public BotTacticalDeviceController? Tactical { get; private set; }
        public HearingDamageComponent? HearingDamage { get; private set; }
        public CombatStateMachine? Combat { get; private set; }
        public BotPoseController? PoseController { get; set; }
        public BotTilt? Tilt { get; private set; }
        public BotOwnerPathfindingCache? PathCache { get; private set; }
        public SquadPathCoordinator? SquadPath { get; private set; }
        public BotTacticalMemory? TacticalMemory { get; private set; }
        public BotLootScanner? LootScanner { get; private set; }
        public BotDeadBodyScanner? DeadBodyScanner { get; private set; }
        public BotDoorOpener? DoorOpener { get; private set; }

        public SquadPathCoordinator? Pathing => SquadPath;

        #endregion

        #region Tactical Modules

        public BotThreatSelector? ThreatSelector { get; private set; }
        public BotInjurySystem? InjurySystem { get; private set; }
        public BotLastShotTracker? LastShotTracker { get; private set; }
        public BotGroupComms? GroupComms { get; private set; }

        #endregion

        #region Squad Healing

        public BotHealAnotherTarget? SquadHealer { get; private set; }
        public BotHealingBySomebody? HealReceiver { get; private set; }

        #endregion

        #region Perception Memory

        public bool IsBlinded { get; set; }
        public float BlindUntilTime { get; set; }
        public float LastFlashTime { get; set; }

        public TrackedEnemyVisibility? VisibilityTracker;

        private float _lastHeardTime = -999f;
        private Vector3? _lastHeardDirection;

        public Vector3? LastHeardDirection => _lastHeardDirection;
        public float LastHeardTime => _lastHeardTime;

        public void RegisterHeardSound(Vector3 source)
        {
            if (Bot?.GetPlayer?.IsAI == true)
            {
                _lastHeardTime = Time.time;
                _lastHeardDirection = source - Bot.Position;
            }
        }

        #endregion

        #region Shortcuts

        public Vector3 Position => Bot?.Position ?? Vector3.zero;
        public BotMemoryClass? Memory => Bot?.Memory;
        public BotPanicHandler? Panic => PanicHandler;
        public string Nickname => Bot?.Profile?.Info?.Nickname ?? "Unknown";

        public bool HasPersonalityTrait(Func<BotPersonalityProfile, bool> predicate)
        {
            return AIRefactoredBotOwner?.PersonalityProfile != null &&
                   predicate(AIRefactoredBotOwner.PersonalityProfile);
        }

        public bool IsReady =>
            Bot != null &&
            Movement != null &&
            PanicHandler != null &&
            Suppression != null &&
            FlashGrenade != null &&
            Tactical != null;

        #endregion

        #region Initialization

        public void Initialize(BotOwner bot)
        {
            if (bot == null) return;
            Bot = bot;

            FlashGrenade = new FlashGrenadeComponent(); FlashGrenade.Initialize(this);
            PanicHandler = new BotPanicHandler(); PanicHandler.Initialize(this);
            Suppression = new BotSuppressionReactionComponent(); Suppression.Initialize(this);
            Escalation = new BotThreatEscalationMonitor(); Escalation.Initialize(bot);
            GroupBehavior = new BotGroupBehavior(); GroupBehavior.Initialize(this);
            Movement = new BotMovementController(); Movement.Initialize(this);
            Tactical = new BotTacticalDeviceController(); Tactical.Initialize(this);
            HearingDamage = new HearingDamageComponent();
            Combat = new CombatStateMachine(); Combat.Initialize(this);
            Tilt = new BotTilt(bot);
            PathCache = new BotOwnerPathfindingCache();
            SquadPath = new SquadPathCoordinator(); SquadPath.Initialize(this);
            TacticalMemory = new BotTacticalMemory(); TacticalMemory.Initialize(this);
            LootScanner = new BotLootScanner(); LootScanner.Initialize(this);
            DeadBodyScanner = new BotDeadBodyScanner(); DeadBodyScanner.Initialize(this);
            DoorOpener = new BotDoorOpener(bot);

            ThreatSelector = new BotThreatSelector(this);
            InjurySystem = new BotInjurySystem(this);
            LastShotTracker = new BotLastShotTracker();
            GroupComms = new BotGroupComms(this);

            SquadHealer = new BotHealAnotherTarget(bot);
            HealReceiver = new BotHealingBySomebody(bot);

            Logger.LogDebug($"[BotComponentCache] Initialized cache for: {Nickname}");
        }

        public void SetOwner(AIRefactoredBotOwner owner)
        {
            if (owner != null)
                AIRefactoredBotOwner = owner;
        }

        #endregion

        #region Reset

        public void Reset()
        {
            IsBlinded = false;
            BlindUntilTime = 0f;
            LastFlashTime = 0f;
            _lastHeardTime = -999f;
            _lastHeardDirection = null;
            VisibilityTracker = null;
            PathCache?.Clear();
        }

        #endregion

        #region Debug

        public void DebugPrint()
        {
            Logger.LogInfo($"[BotComponentCache] {Nickname} | Ready={IsReady} | Blinded={IsBlinded} | HeardSound={LastHeardTime:0.00}s ago");
        }

        #endregion
    }
}
