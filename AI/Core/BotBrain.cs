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

        private const float LocalIdleTickRate = 0.333f;
        private const float LocalCombatTickRate = 1f / 30f;
        private const float HeadlessIdleTickRate = 1f / 30f;
        private const float HeadlessCombatTickRate = 1f / 60f;

        private void Update()
        {
            if (!IsValid())
            {
                enabled = false;
                return;
            }

            float now = Time.time;
            float delta = Time.deltaTime;

            _vision?.Tick(now);
            _perception?.Tick(delta);
            _hearing?.Tick(now);
            _movement?.Tick(delta);
            _groupBehavior?.Tick(delta);
            _cornerScanner?.Tick(now);
            _poseController?.Tick(now);
            _tilt?.ManualUpdate();

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

            _cache = new BotComponentCache();
            _cache.Initialize(bot);

            if (!IsValid())
            {
                enabled = false;
                return;
            }

            // === Logic Systems (pure C# only) ===
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

            _hearingDamage = new HearingDamageComponent();
            _cornerScanner = new BotCornerScanner(bot, _cache);
            _poseController = new BotPoseController(bot, _cache);
            _tilt = _player.GetComponent<BotTilt>() ?? new BotTilt(bot);

            _asyncProcessor = new BotAsyncProcessor();
            _asyncProcessor.Initialize(bot, _cache);

            _teamLogic = new BotTeamLogic(bot);

            if (FikaHeadlessDetector.IsHeadless)
                BotWorkScheduler.RegisterBot(this);
        }

        private void Tick(float time, float deltaTime)
        {
            _mission?.Tick(time);
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
            _asyncProcessor?.Tick(time);
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
