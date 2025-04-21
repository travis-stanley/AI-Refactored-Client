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
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Threads
{
    /// <summary>
    /// Central AI controller for bots in the AIRefactored system.
    /// Manages perception, combat, logic, movement, and group behavior through subsystem delegation.
    /// </summary>
    public class BotBrain : MonoBehaviour
    {
        #region Fields

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

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

        private bool _isValid;

        private float _nextPerceptionTick;
        private float _nextCombatTick;
        private float _nextLogicTick;

        private float PerceptionTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 60f : 1f / 30f;
        private float CombatTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 60f : 1f / 30f;
        private float LogicTickRate => FikaHeadlessDetector.IsHeadless ? 1f / 30f : 1f / 15f;

        #endregion

        #region Unity Lifecycle

        private void Update()
        {
            if (!_isValid)
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

            _movement?.Tick(delta);
            _groupBehavior?.Tick(delta);
            _cornerScanner?.Tick(now);
            _poseController?.Tick(now);
            _tilt?.ManualUpdate();
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes all AI subsystems and verifies dependencies.
        /// </summary>
        /// <param name="bot">The BotOwner instance for this bot.</param>
        public void Initialize(BotOwner bot)
        {
            _bot = bot;
            _player = bot.GetPlayer;

            if (_player == null)
            {
                Logger.LogWarning("[BotBrain] ❌ Player is null during Initialize.");
                enabled = false;
                return;
            }

            var builtCache = new BotComponentCache();
            builtCache.Initialize(bot);
            _cache = builtCache;

            // Re-check nulls explicitly for analyzer satisfaction
            if (_cache == null || _bot == null || _player == null || !_player.IsAI || _player.IsYourPlayer || _bot.IsDead)
            {
                Logger.LogWarning("[BotBrain] ❌ Initialization failed due to invalid state.");
                enabled = false;
                return;
            }

            _isValid = true;

            try
            {
                _combat = new CombatStateMachine();
                _combat.Initialize(_cache);

                _mission = new BotMissionSystem();
                _mission.Initialize(_bot);

                _behavior = new BotBehaviorEnhancer();
                _behavior.Initialize(_cache);

                _escalation = new BotThreatEscalationMonitor();
                _escalation.Initialize(_bot);

                _vision = new BotVisionSystem();
                _vision.Initialize(_cache);

                _hearing = new BotHearingSystem();
                _hearing.Initialize(_cache);

                _perception = new BotPerceptionSystem();
                _perception.Initialize(_cache);

                _movement = new BotMovementController();
                _movement.Initialize(_cache);

                _groupBehavior = new BotGroupBehavior();
                _groupBehavior.Initialize(_cache);

                _lootScanner = new BotLootScanner();
                _lootScanner.Initialize(_cache);

                _corpseScanner = new BotDeadBodyScanner();
                _corpseScanner.Initialize(_cache);

                _flashReaction = new BotFlashReactionComponent();
                _flashReaction.Initialize(_cache);

                _flashDetector = new FlashGrenadeComponent();
                _flashDetector.Initialize(_cache);

                _groupSync = new BotGroupSyncCoordinator();
                _groupSync.Initialize(_bot);

                _tactical = new BotTacticalDeviceController();
                _tactical.Initialize(_bot, _cache);

                _hearingDamage = new HearingDamageComponent();

                _cornerScanner = new BotCornerScanner(_bot, _cache);
                _poseController = new BotPoseController(_bot, _cache);

                _tilt = _player.GetComponent<BotTilt>() ?? new BotTilt(_bot);

                _asyncProcessor = new BotAsyncProcessor();
                _asyncProcessor.Initialize(_bot, _cache);

                _teamLogic = new BotTeamLogic(_bot);

                if (FikaHeadlessDetector.IsHeadless)
                    BotWorkScheduler.RegisterBot(this);

                BotBrainGuardian.Enforce(_player.gameObject);

                Logger.LogInfo($"[BotBrain] ✅ AI stack initialized for bot: {_player.Profile?.Info?.Nickname ?? "Unnamed"}");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[BotBrain] ❌ Exception during bot initialization: {ex}");
                enabled = false;
            }
        }

        #endregion

        #region Tick Delegates

        /// <summary>
        /// Ticks vision and hearing systems for real-time perception.
        /// </summary>
        /// <param name="time">Current world time.</param>
        public void TickPerception(float time)
        {
            _vision?.Tick(time);
            _hearing?.Tick(time);
        }

        /// <summary>
        /// Ticks combat logic, including suppression and flash awareness.
        /// </summary>
        /// <param name="time">Current world time.</param>
        public void TickCombat(float time)
        {
            _combat?.Tick(time);
            _escalation?.Tick(time);
            _flashReaction?.Tick(time);
            _flashDetector?.Tick(time);
        }

        /// <summary>
        /// Ticks high-level mission logic and tactical actions.
        /// </summary>
        /// <param name="time">Current world time.</param>
        public void TickLogic(float time)
        {
            _mission?.Tick(time);
            _behavior?.Tick(time);
            _groupSync?.Tick(time);
            _hearingDamage?.Tick(Time.deltaTime);

            if (_bot != null && _cache != null)
                _tactical?.UpdateTacticalLogic(_bot, _cache);

            _lootScanner?.Tick(Time.deltaTime);
            _corpseScanner?.Tick(time);
            _asyncProcessor?.Tick(time);
        }

        #endregion
    }
}
