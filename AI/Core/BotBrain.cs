#nullable enable

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
using System;
using UnityEngine;

namespace AIRefactored.AI.Threads
{
    /// <summary>
    /// Central AI controller. Ticks perception, combat, movement, group sync, and personality logic.
    /// </summary>
    public sealed class BotBrain : MonoBehaviour
    {
        #region Static

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Fields

        private BotOwner _bot = null!;
        private Player _player = null!;
        private BotComponentCache _cache = null!;

        private bool _isValid;

        private CombatStateMachine? _combat;
        private BotMovementController? _movement;
        private BotPoseController? _pose;
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

        private BotMissionSystem? _mission;
        private BotGroupSyncCoordinator? _groupSync;
        private BotTeamLogic? _teamLogic;
        private BotAsyncProcessor? _async;

        private float _nextPerceptionTick;
        private float _nextCombatTick;
        private float _nextLogicTick;

        private float PerceptionTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 60f : 1f / 30f;
        private float CombatTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 60f : 1f / 30f;
        private float LogicTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 30f : 1f / 15f;

        #endregion

        #region Unity

        private void Update()
        {
            if (!_isValid || _bot == null || _bot.IsDead || _player == null)
                return;

            float now = Time.time;

            if (now >= _nextPerceptionTick)
            {
                _vision?.Tick(now);
                _hearing?.Tick(now);
                _perception?.Tick(Time.deltaTime);
                _nextPerceptionTick = now + PerceptionTickRate;
            }

            if (now >= _nextCombatTick)
            {
                _combat?.Tick(now);
                _cache.Escalation?.Tick(now);
                _flashReaction?.Tick(now);
                _flashDetector?.Tick(now);
                _nextCombatTick = now + CombatTickRate;
            }

            if (now >= _nextLogicTick)
            {
                _mission?.Tick(now);
                _groupSync?.Tick(now);
                _hearingDamage?.Tick(Time.deltaTime);
                _tactical?.Tick();
                _cache.LootScanner?.Tick(Time.deltaTime);
                _cache.DeadBodyScanner?.Tick(now);
                _async?.Tick(now);
                _nextLogicTick = now + LogicTickRate;
            }

            // Movement and tactical response layers
            _movement?.Tick(Time.deltaTime);
            _jump?.Tick(Time.deltaTime);
            _pose?.Tick(now);
            _corner?.Tick(now);
            _tilt?.ManualUpdate();
            _groupBehavior?.Tick(Time.deltaTime);
            _teamLogic?.CoordinateMovement();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Fully initializes the AI system stack for the specified bot.
        /// </summary>
        public void Initialize(BotOwner bot)
        {
            if (bot.GetPlayer == null || bot.IsDead || !bot.GetPlayer.IsAI || bot.GetPlayer.IsYourPlayer)
            {
                Logger.LogWarning("[BotBrain] ❌ Invalid bot context — initialization skipped.");
                return;
            }

            _bot = bot;
            _player = bot.GetPlayer;

            try
            {
                _cache = new BotComponentCache();
                _cache.Initialize(bot);

                if (_player.AIData is AIRefactoredBotOwner owner)
                    _cache.SetOwner(owner);

                _combat = _cache.Combat;
                _movement = _cache.Movement;
                _pose = _cache.PoseController;
                _tilt = _cache.Tilt;
                _tactical = _cache.Tactical;
                _groupBehavior = _cache.GroupBehavior;
                _jump = new BotJumpController(bot, _cache);

                _vision = new BotVisionSystem(); _vision.Initialize(_cache);
                _hearing = new BotHearingSystem(); _hearing.Initialize(_cache);
                _perception = new BotPerceptionSystem(); _perception.Initialize(_cache);
                _flashReaction = new BotFlashReactionComponent(); _flashReaction.Initialize(_cache);
                _flashDetector = new FlashGrenadeComponent(); _flashDetector.Initialize(_cache);
                _hearingDamage = new HearingDamageComponent();
                _corner = new BotCornerScanner();

                _mission = new BotMissionSystem(); _mission.Initialize(bot);
                _groupSync = new BotGroupSyncCoordinator(); _groupSync.Initialize(bot);
                _groupSync.InjectLocalCache(_cache);
                _async = new BotAsyncProcessor(); _async.Initialize(bot, _cache);
                _teamLogic = new BotTeamLogic(bot);

                BotBrainGuardian.Enforce(_player.gameObject);

                _isValid = true;
                Logger.LogInfo($"[BotBrain] ✅ AI stack initialized for bot: {_player.Profile?.Info?.Nickname ?? "Unnamed"}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotBrain] ❌ Initialization failed: {ex.Message}\n{ex.StackTrace}");
                _isValid = false;
            }
        }

        #endregion
    }
}
