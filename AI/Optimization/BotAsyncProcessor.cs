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
    /// Intended to stagger tuning logic outside the spawn frame.
    /// </summary>
    public class BotAsyncProcessor : MonoBehaviour
    {
        private BotOwner? _bot;
        private bool _hasInitialized;
        private float _initDelay = 0.5f;

        private async void Start()
        {
            await Task.Delay((int)(_initDelay * 1000f));

            if (_bot != null)
                await ProcessBotOwnerAsync(_bot);
        }

        /// <summary>
        /// Attach this processor to a bot and begin deferred initialization.
        /// </summary>
        public void Initialize(BotOwner botOwner)
        {
            _bot = botOwner;
        }

        /// <summary>
        /// Entry point for deferred personality tuning.
        /// </summary>
        public async Task ProcessBotOwnerAsync(BotOwner botOwner)
        {
            if (botOwner?.Settings?.FileSettings?.Mind == null || _hasInitialized)
                return;

            await Task.Yield(); // Yield to next frame

            ApplyPersonalityModifiers(botOwner);
            _hasInitialized = true;
        }

        private void ApplyPersonalityModifiers(BotOwner botOwner)
        {
            var mind = botOwner.Settings.FileSettings.Mind;
            var personality = BotRegistry.Get(botOwner.Profile.Id);

            if (personality == null)
                return;

            // Panic reaction tuning
            mind.PANIC_RUN_WEIGHT = Mathf.Lerp(0.5f, 2f, personality.RiskTolerance);
            mind.PANIC_SIT_WEIGHT = Mathf.Lerp(10f, 80f, 1f - personality.RiskTolerance);

            // Squad cohesion (vision range and search radius)
            mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(200f, 600f, 1f - personality.Cohesion);

            // Aggression impulse from teammates
            mind.FRIEND_AGR_KILL = Mathf.Lerp(0f, 0.4f, personality.AggressionLevel);

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored-Async] Tuned bot: {botOwner.Profile?.Info?.Nickname ?? "?"}");
#endif
        }
    }
}
