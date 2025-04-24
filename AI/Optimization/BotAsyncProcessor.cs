#nullable enable

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
    /// Handles asynchronous low-frequency logic for bot behavior tuning and mission processing.
    /// Supports thread-based workloads in headless environments.
    /// </summary>
    public class BotAsyncProcessor
    {
        #region Fields

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotMissionSystem? _mission;
        private BotOwnerStateCache? _stateCache;

        private bool _hasInitialized;
        private float _lastThinkTime;

        private const float InitDelaySeconds = 0.5f;
        private const float ThinkCooldownHeadless = 1.5f;
        private const float ThinkCooldownNormal = 3.5f;

        private readonly BotOwnerGroupOptimization _groupOptimizer = new();

        #endregion

        #region Initialization

        public void Initialize(BotOwner botOwner, BotComponentCache cache)
        {
            _bot = botOwner;
            _cache = cache;
            _stateCache = new BotOwnerStateCache();

            _mission = new BotMissionSystem();
            _mission.Initialize(botOwner);

            Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(InitDelaySeconds));
                if (_bot != null && !_hasInitialized)
                    await ApplyInitialPersonalityAsync(_bot);
            });
        }

        private async Task ApplyInitialPersonalityAsync(BotOwner botOwner)
        {
            if (_hasInitialized || botOwner.Settings?.FileSettings?.Mind == null)
                return;

            await Task.Yield();
            ApplyPersonalityModifiers(botOwner);
            _hasInitialized = true;
        }

        #endregion

        #region Runtime Tick

        public void Tick(float time)
        {
            if (!_hasInitialized || _bot == null || _bot.IsDead)
                return;

            _stateCache?.UpdateBotOwnerStateIfNeeded(_bot);

            TryOptimizeGroup();

            float cooldown = FikaHeadlessDetector.IsHeadless ? ThinkCooldownHeadless : ThinkCooldownNormal;
            if (time - _lastThinkTime < cooldown)
                return;

            _lastThinkTime = time;

            if (FikaHeadlessDetector.IsHeadless)
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        Think();
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[AIRefactored] ⚠️ Headless task failed: {ex}");
                    }
                });
            }
            else
            {
                Think();
            }
        }

        private void Think()
        {
            float now = Time.time;

            _mission?.Tick(now);

            if (_bot == null || _bot.IsDead)
                return;

            if (UnityEngine.Random.value < 0.008f)
            {
                BotWorkScheduler.EnqueueToMainThread(() =>
                {
                    try
                    {
                        _bot?.GetPlayer?.Say(EPhraseTrigger.MumblePhrase);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogWarning($"[AIRefactored] ⚠️ VO mumble failed: {ex.Message}");
                    }
                });
            }
        }

        #endregion

        #region Personality Modifiers

        private void ApplyPersonalityModifiers(BotOwner botOwner)
        {
            var mind = botOwner.Settings?.FileSettings?.Mind;
            string profileId = botOwner.Profile?.Id ?? string.Empty;

            if (mind == null || string.IsNullOrEmpty(profileId))
                return;

            var personality = BotRegistry.Get(profileId);
            if (personality == null)
                return;

            mind.PANIC_RUN_WEIGHT = Mathf.Lerp(0.5f, 2.0f, personality.RiskTolerance);
            mind.PANIC_SIT_WEIGHT = Mathf.Lerp(10.0f, 80.0f, 1f - personality.RiskTolerance);
            mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(200f, 600f, 1f - personality.Cohesion);
            mind.FRIEND_AGR_KILL = Mathf.Lerp(0.0f, 0.4f, personality.AggressionLevel);

            Logger.LogInfo($"[AIRefactored] ✅ Applied personality to bot: {BotName()}");
        }

        #endregion

        #region Group Handling

        private void TryOptimizeGroup()
        {
            if (_bot?.Profile?.Info?.GroupId is not { Length: > 0 } groupId)
                return;

            var teammates = BotTeamTracker.GetGroup(groupId);
            _groupOptimizer.OptimizeGroupAI(teammates);
        }

        private string BotName() =>
            _bot?.Profile?.Info?.Nickname ?? "UnknownBot";

        #endregion
    }
}
