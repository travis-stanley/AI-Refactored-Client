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

        // Tick timing
        private float _nextLogicTick = 0f;
        private float _nextCombatTick = 0f;
        private float _nextPerceptionTick = 0f;

        // === TICK RATES ===

        private const float LocalLogicTickRate = 1f / 15f;
        private const float LocalCombatTickRate = 1f / 30f;
        private const float LocalPerceptionTickRate = 1f / 30f;

        private const float HeadlessLogicTickRate = 1f / 30f;
        private const float HeadlessCombatTickRate = 1f / 60f;
        private const float HeadlessPerceptionTickRate = 1f / 60f;

        private void Update()
        {
            if (!IsValid())
            {
                enabled = false;
                return;
            }

            float now = Time.time;
            float delta = Time.deltaTime;

            // Real-time systems (not ticked)
            _movement?.Tick(delta);
            _groupBehavior?.Tick(delta);
            _cornerScanner?.Tick(now);
            _poseController?.Tick(now);
            _tilt?.ManualUpdate();

            // === PERCEPTION ===
            float perceptionTickRate = FikaHeadlessDetector.IsHeadless ? HeadlessPerceptionTickRate : LocalPerceptionTickRate;
            if (now >= _nextPerceptionTick)
            {
                _vision?.Tick(now);
                _hearing?.Tick(now);
                _perception?.Tick(delta);
                _nextPerceptionTick = now + perceptionTickRate;
            }

            // === COMBAT ===
            float combatTickRate = FikaHeadlessDetector.IsHeadless ? HeadlessCombatTickRate : LocalCombatTickRate;
            if (now >= _nextCombatTick)
            {
                _combat?.Tick(now);
                _escalation?.Tick(now);
                _flashReaction?.Tick(now);
                _flashDetector?.Tick(now);
                _hearingDamage?.Tick(delta);
                _tactical?.UpdateTacticalLogic(_bot!, _cache!);
                _nextCombatTick = now + combatTickRate;
            }

            // === LOGIC ===
            float logicTickRate = FikaHeadlessDetector.IsHeadless ? HeadlessLogicTickRate : LocalLogicTickRate;
            if (now >= _nextLogicTick)
            {
                _mission?.Tick(now);
                _behavior?.Tick(now);
                _groupSync?.Tick(now);
                _lootScanner?.Tick(delta);
                _corpseScanner?.Tick(now);
                _asyncProcessor?.Tick(now);
                _nextLogicTick = now + logicTickRate;
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

            _combat = new CombatStateMachine(); _combat.Initialize(_cache);
            _mission = new BotMissionSystem(); _mission.Initialize(_bot);
            _behavior = new BotBehaviorEnhancer(); _behavior.Initialize(_cache);
            _escalation = new BotThreatEscalationMonitor(); _escalation.Initialize(_bot);

            _vision = new BotVisionSystem(); _vision.Initialize(_cache);
            _hearing = new BotHearingSystem(); _hearing.Initialize(_cache);
            _perception = new BotPerceptionSystem(); _perception.Initialize(_cache);

            _movement = new BotMovementController(); _movement.Initialize(_cache);
            _groupBehavior = new BotGroupBehavior(); _groupBehavior.Initialize(_cache);

            _lootScanner = new BotLootScanner(); _lootScanner.Initialize(_cache);
            _corpseScanner = new BotDeadBodyScanner(); _corpseScanner.Initialize(_cache);

            _flashReaction = new BotFlashReactionComponent(); _flashReaction.Initialize(_cache);
            _flashDetector = new FlashGrenadeComponent(); _flashDetector.Initialize(_cache);

            _groupSync = new BotGroupSyncCoordinator(); _groupSync.Initialize(_bot);
            _tactical = new BotTacticalDeviceController(); _tactical.Initialize(_bot, _cache);
            _hearingDamage = new HearingDamageComponent();

            _cornerScanner = new BotCornerScanner(bot, _cache);
            _poseController = new BotPoseController(bot, _cache);
            _tilt = _player.GetComponent<BotTilt>() ?? new BotTilt(bot);

            _asyncProcessor = new BotAsyncProcessor();
            _asyncProcessor.Initialize(_bot, _cache);

            _teamLogic = new BotTeamLogic(bot);

            if (FikaHeadlessDetector.IsHeadless)
                BotWorkScheduler.RegisterBot(this);
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
