#nullable enable

using EFT;
using System.Threading.Tasks;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Performs async-safe, low-frequency personality initialization for bots.
    /// Intended to stagger tuning logic outside the spawn frame without sacrificing realism.
    /// </summary>
    public class BotAsyncProcessor : MonoBehaviour
    {
        #region Fields

        /// <summary>
        /// The bot associated with this processor.
        /// </summary>
        private BotOwner? _bot;

        /// <summary>
        /// Whether initialization has already been applied to this bot.
        /// </summary>
        private bool _hasInitialized;

        /// <summary>
        /// Initial delay in seconds before async tuning starts.
        /// </summary>
        private const float InitDelay = 0.5f;

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
        /// Injects the bot reference for delayed tuning.
        /// </summary>
        /// <param name="botOwner">The bot to process.</param>
        public void Initialize(BotOwner botOwner)
        {
            _bot = botOwner;
        }

        /// <summary>
        /// Runs delayed personality-based tuning logic for the bot.
        /// </summary>
        /// <param name="botOwner">The bot to process.</param>
        public async Task ProcessBotOwnerAsync(BotOwner botOwner)
        {
            if (_hasInitialized || botOwner?.Settings?.FileSettings?.Mind == null)
                return;

            await Task.Yield(); // Defer for spawn stabilization
            ApplyPersonalityModifiers(botOwner);
            _hasInitialized = true;
        }

        #endregion

        #region Personality Logic

        /// <summary>
        /// Applies bot personality tuning to MindSettings.
        /// </summary>
        /// <param name="botOwner">Bot to tune.</param>
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
