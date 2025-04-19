#nullable enable

using System;
using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Missions;
using AIRefactored.AI.Behavior;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Movement;
using AIRefactored.AI.Components;

namespace AIRefactored.AI.Threads
{
    /// <summary>
    /// Central AI coordinator per bot. Updates all AIRefactored logic systems from a single ticking MonoBehaviour.
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

        private float _nextTick = 0f;
        private const float TickRate = 0.333f;

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
            _movement?.Tick(delta);
            _groupBehavior?.Tick(delta);

            // === Lean reset logic
            _cache?.Tilt?.ManualUpdate();

            if (now >= _nextTick)
            {
                Tick(now, delta);
                _nextTick = now + TickRate;
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
        }

        private void Tick(float time, float deltaTime)
        {
            _mission?.ManualTick(time);
            _behavior?.Tick(time);
            _escalation?.Tick(time);
            _hearing?.Tick(time);
            _flashReaction?.Tick(time);
            _flashDetector?.Tick(time);
            _groupSync?.Tick(time);
            _hearingDamage?.Tick(deltaTime);
            _tactical?.UpdateTacticalLogic(_bot!, _cache!);
        }
    }
}
