﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

#nullable enable

namespace AIRefactored.AI.Threads
{
    using System;
    using AIRefactored.AI.Combat;
    using AIRefactored.AI.Components;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.AI.Missions;
    using AIRefactored.AI.Movement;
    using AIRefactored.AI.Optimization;
    using AIRefactored.AI.Perception;
    using AIRefactored.AI.Reactions;
    using AIRefactored.Core;
    using AIRefactored.Runtime;
    using BepInEx.Logging;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Core AI tick controller for AIRefactored bots.
    /// Runs subsystem logic in real-time for combat, movement, perception, and group logic.
    /// </summary>
    public sealed class BotBrain : MonoBehaviour
    {
        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private BotOwner? _bot;
        private Player? _player;
        private BotComponentCache? _cache;

        private bool _isValid;

        private CombatStateMachine? _combat;
        private BotMovementController? _movement;
        private BotPoseController? _pose;
        private BotLookController? _look;
        private BotTilt? _tilt;
        private BotCornerScanner? _corner;
        private BotGroupBehavior? _groupBehavior;
        private BotJumpController? _jump;
        private BotVisionSystem? _vision;
        private BotHearingSystem? _hearing;
        private BotPerceptionSystem? _perception;
        private HearingDamageComponent? _hearingDamage;
        private FlashGrenadeComponent? _flashDetector;
        private BotFlashReactionComponent? _flashReaction;
        private BotTacticalDeviceController? _tactical;
        private BotMissionController? _mission;
        private BotGroupSyncCoordinator? _groupSync;
        private BotTeamLogic? _teamLogic;
        private BotAsyncProcessor? _asyncProcessor;

        private float _nextPerceptionTick;
        private float _nextCombatTick;
        private float _nextLogicTick;

        private float PerceptionTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 60f : 1f / 30f;
        private float CombatTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 60f : 1f / 30f;
        private float LogicTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 30f : 1f / 15f;

        private void Update()
        {
            if (!GameWorldHandler.IsLocalHost() || !_isValid || _bot == null || _bot.IsDead || _player == null)
            {
                return;
            }

            var currentPlayer = _bot.GetPlayer;
            if (currentPlayer == null
                || currentPlayer.HealthController == null
                || !currentPlayer.HealthController.IsAlive)
            {
                _isValid = false;
                Logger.LogWarning("[BotBrain] Bot invalidated at runtime.");
                return;
            }

            float now = Time.time;
            float delta = Time.deltaTime;

            if (now >= _nextPerceptionTick)
            {
                _vision?.Tick(now);
                _hearing?.Tick(now);
                _perception?.Tick(delta);
                _nextPerceptionTick = now + PerceptionTickRate;
            }

            if (now >= _nextCombatTick)
            {
                _combat?.Tick(now);
                _cache?.Escalation?.Tick(now);
                _flashReaction?.Tick(now);
                _flashDetector?.Tick(now);
                _groupSync?.Tick(now);
                _teamLogic?.CoordinateMovement();
                _nextCombatTick = now + CombatTickRate;
            }

            if (now >= _nextLogicTick)
            {
                _mission?.Tick(now);
                _hearingDamage?.Tick(delta);
                _tactical?.Tick();
                _cache?.LootScanner?.Tick(delta);
                _cache?.DeadBodyScanner?.Tick(now);
                _asyncProcessor?.Tick(now);
                _nextLogicTick = now + LogicTickRate;
            }

            _movement?.Tick(delta);
            _jump?.Tick(delta);
            _pose?.Tick(now);
            _look?.Tick(delta);
            _corner?.Tick(now);
            _tilt?.ManualUpdate();
            _groupBehavior?.Tick(delta);
        }

        /// <summary>
        /// Initializes this Brain for the given BotOwner.
        /// </summary>
        public void Initialize(BotOwner bot)
        {
            if (!GameWorldHandler.IsLocalHost())
            {
                Logger.LogWarning("[BotBrain] Initialization skipped: not authoritative.");
                return;
            }

            // validate input before we store it
            if (bot.GetPlayer == null || bot.IsDead || !bot.GetPlayer.IsAI || bot.GetPlayer.IsYourPlayer)
            {
                Logger.LogWarning("[BotBrain] Initialization rejected: invalid or real player.");
                return;
            }

            _bot = bot;
            _player = bot.GetPlayer;

            try
            {
                GameObject obj = _player.gameObject;

                // --- BotComponentCache injection with TryGet + safe AddComponent ---
                if (!obj.TryGetComponent<BotComponentCache>(out _cache))
                {
                    try
                    {
                        _cache = obj.AddComponent<BotComponentCache>();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError("[BotBrain] Failed to AddComponent<BotComponentCache>: " + ex);
                        _isValid = false;
                        return;
                    }
                }

                _cache.Initialize(bot);

                if (_cache.Combat == null)
                {
                    Logger.LogError("[BotBrain] Combat logic missing for bot: " + (bot.Profile?.Id ?? "unknown"));
                    _isValid = false;
                    return;
                }

                // optional owner hooking
                if (BotRegistry.TryGetRefactoredOwner(bot.ProfileId) is AIRefactoredBotOwner owner)
                {
                    _cache.SetOwner(owner);
                }

                // cache all subsystems
                _combat = _cache.Combat;
                _movement = _cache.Movement;
                _pose = _cache.PoseController;
                _look = _cache.LookController;
                _tilt = _cache.Tilt;
                _groupBehavior = _cache.GroupBehavior;

                // create or fetch others
                _corner = new BotCornerScanner(bot, _cache);
                _jump = new BotJumpController(bot, _cache);

                _vision = new BotVisionSystem(); _vision.Initialize(_cache);
                _hearing = new BotHearingSystem(); _hearing.Initialize(_cache);
                _perception = new BotPerceptionSystem(); _perception.Initialize(_cache);

                _flashReaction = new BotFlashReactionComponent(); _flashReaction.Initialize(_cache);
                _flashDetector = new FlashGrenadeComponent(); _flashDetector.Initialize(_cache);
                _hearingDamage = new HearingDamageComponent();
                _tactical = _cache.Tactical;
                _mission = new BotMissionController(bot, _cache);

                _groupSync = new BotGroupSyncCoordinator();
                _groupSync.Initialize(bot);
                _groupSync.InjectLocalCache(_cache);

                _asyncProcessor = new BotAsyncProcessor(); _asyncProcessor.Initialize(bot, _cache);
                _teamLogic = new BotTeamLogic(bot);

                // --- ensure we don't accidentally re-add BotBrain on top of ourselves ---
                if (!obj.TryGetComponent<BotBrain>(out _))
                {
                    BotBrainGuardian.Enforce(obj);
                }

                _isValid = true;
                Logger.LogInfo("[BotBrain] ✅ AI initialized for: " + (_player.Profile?.Info?.Nickname ?? "Unnamed"));
            }
            catch (Exception ex)
            {
                Logger.LogError("[BotBrain] Initialization failed: " + ex);
                _isValid = false;
            }
        }
    }
}
