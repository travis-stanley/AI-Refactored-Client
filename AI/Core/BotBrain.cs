#nullable enable

using AIRefactored.AI.Behavior;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using AIRefactored.AI.Group;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Missions;
using AIRefactored.AI.Movement;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Reactions;
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

        private float _nextTick = 0f;
        private const float IdleTickRate = 0.333f;
        private const float CombatTickRate = 1f / 30f;

        private void Update()
        {
            if (_bot == null || _bot.IsDead || _player == null || !_player.IsAI || _player.IsYourPlayer)
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

            bool isAlert =
                _combat?.IsInCombatState() == true ||
                _bot.Memory?.GoalEnemy != null ||
                _cache?.IsBlinded == true ||
                _cache?.LastHeardTime + 2f > now;

            float tickRate = isAlert ? CombatTickRate : IdleTickRate;

            if (now >= _nextTick)
            {
                Tick(now, delta);
                _nextTick = now + tickRate;
            }
        }

        public void Initialize(BotOwner bot)
        {
            _bot = bot;
            _player = bot.GetPlayer;
            _cache = GetComponent<BotComponentCache>();

            if (_player == null || !_player.IsAI || _player.IsYourPlayer || _bot == null || _cache == null)
            {
                enabled = false;
                return;
            }

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

            _cornerScanner = new BotCornerScanner(bot, _cache);
            _poseController = new BotPoseController(bot, _cache);

            _asyncProcessor = gameObject.AddComponent<BotAsyncProcessor>();
            _asyncProcessor.Initialize(bot);

            _tilt = _player.GetComponent<BotTilt>();
            if (_tilt == null)
                _tilt = new BotTilt(bot);

            _teamLogic = new BotTeamLogic(bot);
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
        }
    }
}
