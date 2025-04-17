#nullable enable

using System.Threading.Tasks;
using UnityEngine;
using EFT;
using AIRefactored.AI;
using AIRefactored.Core;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Performs async-safe, low-frequency personality initialization for bots.
    /// Intended to stagger tuning logic outside the spawn frame without sacrificing realism.
    /// </summary>
    public class BotAsyncProcessor : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private bool _hasInitialized;
        private const float InitDelay = 0.5f;

        #endregion

        #region Unity Lifecycle

        private async void Start()
        {
            await Task.Delay((int)(InitDelay * 1000f));

            if (_bot != null && !_hasInitialized)
                await ProcessBotOwnerAsync(_bot);
        }

        #endregion

        #region Initialization

        public void Initialize(BotOwner botOwner)
        {
            _bot = botOwner;
        }

        public async Task ProcessBotOwnerAsync(BotOwner botOwner)
        {
            if (_hasInitialized || botOwner?.Settings?.FileSettings?.Mind == null)
                return;

            await Task.Yield(); // Let spawn-related systems complete

            ApplyPersonalityModifiers(botOwner);
            _hasInitialized = true;
        }

        #endregion

        #region Personality Logic

        private void ApplyPersonalityModifiers(BotOwner botOwner)
        {
            var mind = botOwner.Settings.FileSettings.Mind;
            var personality = BotRegistry.Get(botOwner.Profile.Id);

            if (mind == null || personality == null)
                return;

            // Maintain realism while tuning reactions
            mind.PANIC_RUN_WEIGHT = Mathf.Lerp(0.5f, 2f, personality.RiskTolerance);
            mind.PANIC_SIT_WEIGHT = Mathf.Lerp(10f, 80f, 1f - personality.RiskTolerance);
            mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(200f, 600f, 1f - personality.Cohesion);
            mind.FRIEND_AGR_KILL = Mathf.Lerp(0f, 0.4f, personality.AggressionLevel);

            Debug.Log($"[AIRefactored-Async] ✅ Personality tuning applied → {botOwner.Profile?.Info?.Nickname ?? "Unnamed Bot"}");
        }

        #endregion
    }
}
