#nullable enable

namespace AIRefactored.AI.Optimization
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.Core;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using EFT;

    using UnityEngine;

    /// <summary>
    /// Handles asynchronous low-frequency logic for bot behavior tuning and squad optimization.
    /// Supports thread-based workloads in headless environments.
    /// </summary>
    public class BotAsyncProcessor
    {
        private const float InitDelaySeconds = 0.5f;

        private const float ThinkCooldownHeadless = 1.5f;

        private const float ThinkCooldownNormal = 3.5f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private readonly BotOwnerGroupOptimization _groupOptimizer = new();

        private BotOwner? _bot;

        private BotComponentCache? _cache;

        private bool _hasInitialized;

        private float _lastThinkTime;

        private BotOwnerStateCache? _stateCache;

        public void Initialize(BotOwner botOwner, BotComponentCache cache)
        {
            this._bot = botOwner;
            this._cache = cache;
            this._stateCache = new BotOwnerStateCache();

            Task.Run(async () =>
                {
                    await Task.Delay(TimeSpan.FromSeconds(InitDelaySeconds));
                    if (this._bot != null && !this._hasInitialized)
                        await this.ApplyInitialPersonalityAsync(this._bot);
                });
        }

        public void Tick(float time)
        {
            if (!this._hasInitialized || this._bot == null || this._bot.IsDead)
                return;

            this._stateCache?.UpdateBotOwnerStateIfNeeded(this._bot);

            this.TryOptimizeGroup();

            float cooldown = FikaHeadlessDetector.IsHeadless ? ThinkCooldownHeadless : ThinkCooldownNormal;
            if (time - this._lastThinkTime < cooldown)
                return;

            this._lastThinkTime = time;

            if (FikaHeadlessDetector.IsHeadless)
            {
                ThreadPool.QueueUserWorkItem((object _) =>
                    {
                        try
                        {
                            this.Think();
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[AIRefactored] ⚠️ Headless task failed: {ex}");
                        }
                    });
            }
            else
            {
                this.Think();
            }
        }

        private async Task ApplyInitialPersonalityAsync(BotOwner botOwner)
        {
            if (this._hasInitialized || botOwner.Settings?.FileSettings?.Mind == null)
                return;

            await Task.Yield();
            this.ApplyPersonalityModifiers(botOwner);
            this._hasInitialized = true;
        }

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

            Logger.LogInfo($"[AIRefactored] ✅ Applied personality to bot: {this.BotName()}");
        }

        private string BotName()
        {
            return this._bot?.Profile?.Info?.Nickname ?? "UnknownBot";
        }

        private void Think()
        {
            if (this._bot == null || this._bot.IsDead)
                return;

            if (UnityEngine.Random.value < 0.008f)
            {
                BotWorkScheduler.EnqueueToMainThread(() =>
                    {
                        try
                        {
                            this._bot?.GetPlayer?.Say(EPhraseTrigger.MumblePhrase);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogWarning($"[AIRefactored] ⚠️ VO mumble failed: {ex.Message}");
                        }
                    });
            }
        }

        private void TryOptimizeGroup()
        {
            if (this._bot?.Profile?.Info?.GroupId is not { Length: > 0 } groupId)
                return;

            var teammates = BotTeamTracker.GetGroup(groupId);
            this._groupOptimizer.OptimizeGroupAI(teammates);
        }
    }
}