#nullable enable

using AIRefactored.AI.Behavior;
using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Missions;
using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Handles asynchronous and low-frequency logic for bot personality tuning and mission processing.
    /// Supports headless environments via thread pooling and optimized tick intervals.
    /// </summary>
    public class BotAsyncProcessor
    {
        #region Fields

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private BotMissionSystem? _mission;
        private BotBehaviorEnhancer? _behavior;
        private BotOwnerStateCache? _stateCache;

        private bool _hasInitialized;
        private float _lastThinkTime = 0f;

        private const float InitDelay = 0.5f;
        private const float HeadlessThinkCooldown = 1.5f;
        private const float NormalThinkCooldown = 3.5f;

        private readonly BotOwnerGroupOptimization _groupOptimizer = new();

        #endregion

        #region Initialization

        /// <summary>
        /// Assigns the bot owner and cache, and defers runtime initialization.
        /// </summary>
        /// <param name="botOwner">The BotOwner instance.</param>
        /// <param name="cache">Initialized bot component cache.</param>
        public void Initialize(BotOwner botOwner, BotComponentCache cache)
        {
            _bot = botOwner;
            _cache = cache;
            _stateCache = new BotOwnerStateCache();

            _mission = new BotMissionSystem();
            _mission.Initialize(botOwner);

            _behavior = new BotBehaviorEnhancer();
            _behavior.Initialize(cache);

            Task.Run(async () =>
            {
                await Task.Delay((int)(InitDelay * 1000f));
                if (_bot != null && !_hasInitialized)
                {
                    await ProcessBotOwnerAsync(_bot);
                }
            });
        }

        /// <summary>
        /// Completes the async personality tuning stage once the bot is loaded.
        /// </summary>
        /// <param name="botOwner">The bot to initialize.</param>
        public async Task ProcessBotOwnerAsync(BotOwner botOwner)
        {
            if (_hasInitialized || botOwner?.Settings?.FileSettings?.Mind == null)
                return;

            await Task.Yield(); // Let Unity thread breathe before continuing

            ApplyPersonalityModifiers(botOwner);
            _hasInitialized = true;
        }

        #endregion

        #region Tick Logic

        /// <summary>
        /// Executes async-aware logic and schedules AI ticks at low frequency.
        /// </summary>
        /// <param name="time">Current world time.</param>
        public void Tick(float time)
        {
            if (!_hasInitialized || _bot == null || _bot.IsDead)
                return;

            _stateCache?.UpdateBotOwnerStateIfNeeded(_bot);

            string? groupId = _bot.Profile?.Info?.GroupId;
            if (!string.IsNullOrEmpty(groupId))
            {
                var teammates = BotTeamTracker.GetGroup(groupId!);
                _groupOptimizer.OptimizeGroupAI(teammates);
            }

            float cooldown = FikaHeadlessDetector.IsHeadless ? HeadlessThinkCooldown : NormalThinkCooldown;
            if (time - _lastThinkTime < cooldown)
                return;

            _lastThinkTime = time;

            if (FikaHeadlessDetector.IsHeadless)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        ThinkThreaded();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[AIRefactored-Async] ❌ Headless thinker crash: {ex}");
                    }
                });
            }
            else
            {
                ThinkThreaded();
            }
        }

        /// <summary>
        /// Called on cooldown to process mission and behavior ticks.
        /// Runs on thread pool if headless.
        /// </summary>
        private void ThinkThreaded()
        {
            float time = Time.time;

            _mission?.Tick(time);
            _behavior?.Tick(time);

            if (_bot != null && UnityEngine.Random.value < 0.01f)
            {
                BotWorkScheduler.EnqueueToMainThread(() =>
                {
                    _bot?.GetPlayer?.Say(EPhraseTrigger.MumblePhrase);
                });
            }
        }

        #endregion

        #region Personality Tuning

        /// <summary>
        /// Applies tuning from personality traits to EFT internal mind parameters.
        /// </summary>
        /// <param name="botOwner">Target bot owner for modification.</param>
        private void ApplyPersonalityModifiers(BotOwner botOwner)
        {
            var mind = botOwner.Settings?.FileSettings?.Mind;
            var profileId = botOwner.Profile?.Id;

            if (mind == null || string.IsNullOrEmpty(profileId))
                return;

            var personality = BotRegistry.Get(profileId!); // Safe due to above null check
            if (personality == null)
                return;

            mind.PANIC_RUN_WEIGHT = Mathf.Lerp(0.5f, 2.0f, personality.RiskTolerance);
            mind.PANIC_SIT_WEIGHT = Mathf.Lerp(10.0f, 80.0f, 1f - personality.RiskTolerance);
            mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(200f, 600f, 1f - personality.Cohesion);
            mind.FRIEND_AGR_KILL = Mathf.Lerp(0.0f, 0.4f, personality.AggressionLevel);

            Logger.LogInfo($"[AIRefactored-Async] ✅ Personality tuned → {botOwner.Profile?.Info?.Nickname ?? "Unnamed Bot"}");
        }

        #endregion
    }
}
