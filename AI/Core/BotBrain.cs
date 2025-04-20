#nullable enable

using AIRefactored.AI.Behavior;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Looting;
using AIRefactored.AI.Missions;
using AIRefactored.AI.Movement;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Reactions;
using AIRefactored.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Threads
{
    /// <summary>
    /// Central brain component for AI bots. Controls tick frequency and dispatches subsystems.
    /// </summary>
    public class BotBrain : MonoBehaviour
    {
        private BotOwner? _bot;
        private Player? _player;
        private BotComponentCache? _cache;

        private BotMissionSystem? _mission;
        private BotBehaviorEnhancer? _behavior;
        private CombatStateMachine? _combat;
        private BotThreatEscalationMonitor? _escalation;
        private BotVisionSystem? _vision;
        private BotHearingSystem? _hearing;
        private BotPerceptionSystem? _perception;
        private BotFlashReactionComponent? _flashReaction;
        private FlashGrenadeComponent? _flashDetector;
        private BotGroupSyncCoordinator? _groupSync;
        private BotGroupBehavior? _groupBehavior;
        private BotMovementController? _movement;
        private BotTacticalDeviceController? _tactical;
        private HearingDamageComponent? _hearingDamage;
        private BotTilt? _tilt;
        private BotCornerScanner? _cornerScanner;
        private BotPoseController? _poseController;
        private BotAsyncProcessor? _asyncProcessor;
        private BotTeamLogic? _teamLogic;
        private BotLootScanner? _lootScanner;
        private BotDeadBodyScanner? _corpseScanner;

        // Tick timestamps
        private float _nextPerceptionTick;
        private float _nextCombatTick;
        private float _nextLogicTick;

        // Tick rates
        private float PerceptionTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 60f : 1f / 30f;
        private float CombatTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 60f : 1f / 30f;
        private float LogicTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 30f : 1f / 15f;

        private void Update()
        {
            if (!IsValid())
            {
                enabled = false;
                return;
            }

            float now = Time.time;

            if (now >= _nextPerceptionTick)
            {
                TickPerception(now);
                _nextPerceptionTick = now + PerceptionTickRate;
            }

            if (now >= _nextCombatTick)
            {
                TickCombat(now);
                _nextCombatTick = now + CombatTickRate;
            }

            if (now >= _nextLogicTick)
            {
                TickLogic(now);
                _nextLogicTick = now + LogicTickRate;
            }

            float delta = Time.deltaTime;

            // Always-realtime systems (movement must never tick-gate)
            _movement?.Tick(delta);
            _groupBehavior?.Tick(delta);
            _cornerScanner?.Tick(now);
            _poseController?.Tick(now);
            _tilt?.ManualUpdate();
        }

        public void Initialize(BotOwner bot)
        {
            _bot = bot;
            _player = bot.GetPlayer;

            _cache = new BotComponentCache();
            _cache.Initialize(bot);

            if (!IsValid())
            {
                enabled = false;
                return;
            }

            // === Pure logic systems ===
            (_combat = new CombatStateMachine()).Initialize(_cache);
            (_mission = new BotMissionSystem()).Initialize(bot);
            (_behavior = new BotBehaviorEnhancer()).Initialize(_cache);
            (_escalation = new BotThreatEscalationMonitor()).Initialize(bot);
            (_vision = new BotVisionSystem()).Initialize(_cache);
            (_hearing = new BotHearingSystem()).Initialize(_cache);
            (_perception = new BotPerceptionSystem()).Initialize(_cache);
            (_movement = new BotMovementController()).Initialize(_cache);
            (_groupBehavior = new BotGroupBehavior()).Initialize(_cache);
            (_lootScanner = new BotLootScanner()).Initialize(_cache);
            (_corpseScanner = new BotDeadBodyScanner()).Initialize(_cache);
            (_flashReaction = new BotFlashReactionComponent()).Initialize(_cache);
            (_flashDetector = new FlashGrenadeComponent()).Initialize(_cache);
            (_groupSync = new BotGroupSyncCoordinator()).Initialize(bot);
            (_tactical = new BotTacticalDeviceController()).Initialize(bot, _cache);

            // === One-off components ===
            _hearingDamage = new HearingDamageComponent();
            _cornerScanner = new BotCornerScanner(bot, _cache);
            _poseController = new BotPoseController(bot, _cache);
            _tilt = _player?.GetComponent<BotTilt>() ?? new BotTilt(bot);
            _asyncProcessor = new BotAsyncProcessor();
            _asyncProcessor.Initialize(bot, _cache);
            _teamLogic = new BotTeamLogic(bot);

            // === Register with scheduler (for headless tick balancing) ===
            if (FikaHeadlessDetector.IsHeadless)
                BotWorkScheduler.RegisterBot(this);
        }

        public void TickPerception(float time)
        {
            _vision?.Tick(time);
            _hearing?.Tick(time);
        }

        public void TickCombat(float time)
        {
            _combat?.Tick(time);
            _escalation?.Tick(time);
            _flashReaction?.Tick(time);
            _flashDetector?.Tick(time);
        }

        public void TickLogic(float time)
        {
            _mission?.Tick(time);
            _behavior?.Tick(time);
            _groupSync?.Tick(time);
            _hearingDamage?.Tick(Time.deltaTime);
            _tactical?.UpdateTacticalLogic(_bot!, _cache!);
            _lootScanner?.Tick(Time.deltaTime);
            _corpseScanner?.Tick(time);
            _asyncProcessor?.Tick(time);
        }

        private bool IsValid()
        {
            return _bot != null &&
                   _player != null &&
                   _player.IsAI &&
                   !_player.IsYourPlayer &&
                   !_bot.IsDead &&
                   _cache != null;
        }
    }
}
