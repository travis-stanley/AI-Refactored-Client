#nullable enable

using AIRefactored.AI.Behavior;
using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Missions;
using AIRefactored.Core;
using EFT;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Performs async-safe, low-frequency personality initialization and runtime optimization.
    /// On headless servers, offloads processing to background threads for higher tick frequency and throughput.
    /// </summary>
    public class BotAsyncProcessor
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private bool _hasInitialized;

        private const float InitDelay = 0.5f;
        private const float HeadlessThinkCooldown = 1.5f;
        private const float NormalThinkCooldown = 3.5f;

        private BotOwnerStateCache? _stateCache;
        private readonly BotOwnerGroupOptimization _groupOptimizer = new();

        private BotMissionSystem? _mission;
        private BotBehaviorEnhancer? _behavior;

        private float _lastThinkTime = 0f;

        /// <summary>
        /// Initializes the async processor with a bot and its component cache.
        /// </summary>
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
                    await ProcessBotOwnerAsync(_bot);
            });
        }

        /// <summary>
        /// Applies tuning logic after bot is fully loaded.
        /// </summary>
        public async Task ProcessBotOwnerAsync(BotOwner botOwner)
        {
            if (_hasInitialized || botOwner?.Settings?.FileSettings?.Mind == null)
                return;

            await Task.Yield();
            ApplyPersonalityModifiers(botOwner);
            _hasInitialized = true;
        }

        /// <summary>
        /// Executes low-frequency logic ticks for squad sync, thinking, and async ops.
        /// </summary>
        public void Tick(float time)
        {
            if (!_hasInitialized || _bot == null || _bot.IsDead)
                return;

            _stateCache?.UpdateBotOwnerStateIfNeeded(_bot);

            var groupId = _bot.Profile?.Info?.GroupId;
            if (!string.IsNullOrEmpty(groupId))
            {
                var teammates = BotTeamTracker.GetGroup(groupId);
                _groupOptimizer.OptimizeGroupAI(teammates);
            }

            float cooldown = FikaHeadlessDetector.IsHeadless ? HeadlessThinkCooldown : NormalThinkCooldown;
            if (time - _lastThinkTime >= cooldown)
            {
                _lastThinkTime = time;

                if (FikaHeadlessDetector.IsHeadless)
                {
                    ThreadPool.QueueUserWorkItem(_ =>
                    {
                        try { ThinkThreaded(); }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[AIRefactored-Async] ❌ Headless thinker crash: {ex.Message}");
                        }
                    });
                }
                else
                {
                    ThinkThreaded();
                }
            }
        }

        /// <summary>
        /// Main decision loop executed either inline or from thread pool.
        /// </summary>
        private void ThinkThreaded()
        {
            _mission?.Tick(Time.time);
            _behavior?.Tick(Time.time);

            if (_bot != null && UnityEngine.Random.value < 0.01f)
            {
                BotWorkScheduler.EnqueueToMainThread(() =>
                {
                    _bot?.GetPlayer?.Say(EPhraseTrigger.MumblePhrase);
                });
            }
        }

        /// <summary>
        /// Applies baseline behavior modifiers from personality to EFT mind parameters.
        /// </summary>
        private void ApplyPersonalityModifiers(BotOwner botOwner)
        {
            var mind = botOwner.Settings.FileSettings.Mind;
            var personality = BotRegistry.Get(botOwner.Profile.Id);

            if (mind == null || personality == null)
                return;

            mind.PANIC_RUN_WEIGHT = Mathf.Lerp(0.5f, 2f, personality.RiskTolerance);
            mind.PANIC_SIT_WEIGHT = Mathf.Lerp(10f, 80f, 1f - personality.RiskTolerance);
            mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(200f, 600f, 1f - personality.Cohesion);
            mind.FRIEND_AGR_KILL = Mathf.Lerp(0f, 0.4f, personality.AggressionLevel);

            Debug.Log($"[AIRefactored-Async] ✅ Personality tuned → {botOwner.Profile?.Info?.Nickname ?? "Unnamed Bot"}");
        }
    }
}
