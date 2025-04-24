#nullable enable

using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat.States
{
    /// <summary>
    /// Handles investigate behavior when a bot hears or senses an enemy without visual contact.
    /// </summary>
    public sealed class InvestigateHandler
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotOwner _bot;
        private readonly BotTacticalMemory _memory;

        private const float ScanRadius = 4f;
        private const float InvestigateCooldown = 10f;
        private const float SoundReactTime = 1.0f;

        #endregion

        #region Constructor

        public InvestigateHandler(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot!;
            _memory = cache.TacticalMemory!;
        }

        #endregion

        #region State Evaluation

        /// <summary>
        /// Determines if the bot should start investigating based on sound memory and caution level.
        /// </summary>
        public bool ShallUseNow(float time, float lastTransition)
        {
            var profile = _cache.AIRefactoredBotOwner?.PersonalityProfile;
            if (profile == null || profile.Caution < 0.3f)
                return false;

            return _cache.LastHeardTime + SoundReactTime > time &&
                   time - lastTransition > 1.25f;
        }

        /// <summary>
        /// Returns true if the bot has finished investigating (i.e., enough time has passed).
        /// </summary>
        public bool ShouldExit(float now, float lastHitTime, float cooldown)
        {
            return now - lastHitTime > cooldown;
        }

        #endregion

        #region Target Acquisition

        /// <summary>
        /// Gets a target position to investigate: enemy memory, last known position, or random offset.
        /// </summary>
        public Vector3? GetInvestigateTarget(Vector3? lastKnownEnemyPos)
        {
            return lastKnownEnemyPos
                ?? _memory.GetRecentEnemyMemory()
                ?? RandomNearbyPosition();
        }

        #endregion

        #region Movement

        /// <summary>
        /// Moves the bot to the given investigate target and marks the position as cleared.
        /// </summary>
        public void Investigate(Vector3 target)
        {
            Vector3 destination = _cache.SquadPath?.ApplyOffsetTo(target) ?? target;
            BotMovementHelper.SmoothMoveTo(_bot, destination);
            _memory.MarkCleared(destination);
            _cache.Combat?.TrySetStanceFromNearbyCover(destination);
        }

        /// <summary>
        /// Generates a random local position within a small scan radius.
        /// </summary>
        private Vector3 RandomNearbyPosition()
        {
            Vector3 jitter = UnityEngine.Random.insideUnitSphere * ScanRadius;
            jitter.y = 0f; // keep flat
            return _bot.Position + jitter;
        }

        #endregion
    }
}
