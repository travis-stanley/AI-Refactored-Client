#nullable enable

using AIRefactored.AI.Groups;
using EFT;
using System.Threading.Tasks;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Performs async-safe, low-frequency personality initialization for bots.
    /// Also executes periodic runtime optimization and squad resync.
    /// </summary>
    public class BotAsyncProcessor : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private bool _hasInitialized;
        private const float InitDelay = 0.5f;

        private BotOwnerStateCache? _stateCache;
        private readonly BotOwnerGroupOptimization _groupOptimizer = new();

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Unity Start method that schedules deferred async initialization.
        /// </summary>
        private async void Start()
        {
            await Task.Delay((int)(InitDelay * 1000f));

            if (_bot != null && !_hasInitialized)
            {
                await ProcessBotOwnerAsync(_bot);
            }
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Injects the bot reference for delayed tuning and runtime optimization.
        /// </summary>
        public void Initialize(BotOwner botOwner)
        {
            _bot = botOwner;
            _stateCache = new BotOwnerStateCache();
        }

        /// <summary>
        /// Runs delayed personality-based tuning logic for the bot.
        /// </summary>
        public async Task ProcessBotOwnerAsync(BotOwner botOwner)
        {
            if (_hasInitialized || botOwner?.Settings?.FileSettings?.Mind == null)
                return;

            await Task.Yield(); // Defer for spawn stabilization
            ApplyPersonalityModifiers(botOwner);
            _hasInitialized = true;
        }

        #endregion

        #region Tick-Based Runtime Optimization

        /// <summary>
        /// Called on slow tick from BotBrain to apply dynamic optimization.
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
        }

        #endregion

        #region Personality Logic

        /// <summary>
        /// Applies bot personality tuning to MindSettings.
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

            Debug.Log($"[AIRefactored-Async] ✅ Personality tuning applied → {botOwner.Profile?.Info?.Nickname ?? "Unnamed Bot"}");
        }

        #endregion
    }
}
