#nullable enable

using AIRefactored.AI.Behavior;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using AIRefactored.AI.Group;
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
    /// Central brain component for AI bots. Ticks vision, hearing, mission, movement, and perception systems.
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

        private float _nextTick = 0f;

        // === Tick Rates ===
        private const float LocalIdleTickRate = 0.333f;     // ~3Hz for idle bots on client
        private const float LocalCombatTickRate = 1f / 30f; // 30Hz for combat state on client
        private const float HeadlessIdleTickRate = 1f / 30f; // 30Hz for idle bots on FIKA headless
        private const float HeadlessCombatTickRate = 1f / 60f; // 60Hz for combat on FIKA headless

        private void Update()
        {
            if (!IsValid())
            {
                enabled = false;
                return;
            }

            float now = Time.time;
            float delta = Time.deltaTime;

            // Always-ticked real-time components
            _vision?.Tick(now);             // 30Hz frame-locked
            _perception?.Tick(delta);      // Real-time
            _hearing?.Tick(now);           // 30Hz frame-locked
            _movement?.Tick(delta);        // Real-time
            _groupBehavior?.Tick(delta);   // Real-time
            _cornerScanner?.Tick(now);     // Real-time
            _poseController?.Tick(now);    // Real-time
            _tilt?.ManualUpdate();         // Real-time

            // Local ticked logic
            if (!FikaHeadlessDetector.IsHeadless)
            {
                float tickRate = ShouldCombatTick(now) ? LocalCombatTickRate : LocalIdleTickRate;
                if (now >= _nextTick)
                {
                    Tick(now, delta);
                    _nextTick = now + tickRate;
                }
            }
        }

        public void Initialize(BotOwner bot)
        {
            _bot = bot;
            _player = bot.GetPlayer;
            _cache = GetComponent<BotComponentCache>();

            if (!IsValid())
            {
                enabled = false;
                return;
            }

            // Component assignments
            _mission = GetComponent<BotMissionSystem>();
            _behavior = GetComponent<BotBehaviorEnhancer>();
            _combat = GetComponent<CombatStateMachine>();
            _escalation = GetComponent<BotThreatEscalationMonitor>();
            _vision = GetComponent<BotVisionSystem>();
            _hearing = GetComponent<BotHearingSystem>();
            _perception = GetComponent<BotPerceptionSystem>();
            _flashReaction = GetComponent<BotFlashReactionComponent>();
            _flashDetector = GetComponent<FlashGrenadeComponent>();
            _groupSync = GetComponent<BotGroupSyncCoordinator>();
            _groupBehavior = GetComponent<BotGroupBehavior>();
            _movement = GetComponent<BotMovementController>();
            _tactical = GetComponent<BotTacticalDeviceController>();
            _hearingDamage = GetComponent<HearingDamageComponent>();
            _lootScanner = GetComponent<BotLootScanner>();
            _corpseScanner = GetComponent<BotDeadBodyScanner>();

            // Procedural logic modules
            _cornerScanner = new BotCornerScanner(bot, _cache);
            _poseController = new BotPoseController(bot, _cache);

            // Always attach async processor
            _asyncProcessor = gameObject.AddComponent<BotAsyncProcessor>();
            _asyncProcessor.Initialize(bot);

            // Create or reuse tilt logic
            _tilt = _player.GetComponent<BotTilt>() ?? new BotTilt(bot);

            // Runtime logic model
            _teamLogic = new BotTeamLogic(bot);

            // Register if FIKA headless
            if (FikaHeadlessDetector.IsHeadless)
                BotWorkScheduler.RegisterBot(this);
        }

        private void Tick(float time, float deltaTime)
        {
            _mission?.ManualTick(time);
            _behavior?.Tick(time);
            _combat?.Tick(time);
            _escalation?.Tick(time);
            _flashReaction?.Tick(time);
            _flashDetector?.Tick(time);
            _groupSync?.Tick(time);
            _hearingDamage?.Tick(deltaTime);
            _tactical?.UpdateTacticalLogic(_bot!, _cache!);
            _lootScanner?.Tick(deltaTime);
            _corpseScanner?.Tick(time);
        }

        public void BackgroundTick(float time)
        {
            if (!IsValid())
                return;

            float deltaTime = Time.deltaTime;
            float tickRate = ShouldCombatTick(time) ? HeadlessCombatTickRate : HeadlessIdleTickRate;

            if (time >= _nextTick)
            {
                Tick(time, deltaTime);
                _nextTick = time + tickRate;
            }
        }

        private bool ShouldCombatTick(float now)
        {
            return
                _combat?.IsInCombatState() == true ||
                _bot?.Memory?.GoalEnemy != null ||
                _cache?.IsBlinded == true ||
                (_cache?.LastHeardTime ?? 0f) + 2f > now;
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
