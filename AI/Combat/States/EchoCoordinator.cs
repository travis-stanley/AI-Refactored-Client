#nullable enable

using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat.States
{
    /// <summary>
    /// Handles squad echoing behavior for fallback, investigate, and enemy sync.
    /// Ensures bots respond to squad member triggers in proximity and act cohesively.
    /// </summary>
    public sealed class EchoCoordinator
    {
        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;

        private float _lastEchoInvestigate;
        private float _lastEchoFallback;
        private const float EchoCooldown = 4f;
        private const float MaxEchoRange = 40f;

        #endregion

        #region Constructor

        public EchoCoordinator(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot!;
        }

        #endregion

        #region Echo Methods

        /// <summary>
        /// Informs nearby squadmates to investigate a disturbance.
        /// </summary>
        public void EchoInvestigateToSquad()
        {
            if (_bot.BotsGroup == null || Time.time - _lastEchoInvestigate < EchoCooldown)
                return;

            for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
            {
                var mate = _bot.BotsGroup.Member(i);
                if (!IsValidSquadmate(mate)) continue;

                var mateCache = BotCacheUtility.GetCache(mate);
                if (mateCache?.Combat == null || !CanAcceptEcho(mateCache))
                    continue;

                mateCache.Combat.NotifyEchoInvestigate();
            }

            _lastEchoInvestigate = Time.time;
        }

        /// <summary>
        /// Orders nearby squadmates to retreat using fallback logic.
        /// </summary>
        public void EchoFallbackToSquad(Vector3 retreatPos)
        {
            if (_bot.BotsGroup == null || Time.time - _lastEchoFallback < EchoCooldown)
                return;

            for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
            {
                var mate = _bot.BotsGroup.Member(i);
                if (!IsValidSquadmate(mate)) continue;

                var mateCache = BotCacheUtility.GetCache(mate);
                if (mateCache?.Combat == null || !CanAcceptEcho(mateCache))
                    continue;

                Vector3 dir = mate.LookDirection.normalized;
                Vector3 fallback = mate.Position - dir * 6f;

                mateCache.Combat.TriggerFallback(fallback);
            }

            _lastEchoFallback = Time.time;
        }

        /// <summary>
        /// Broadcasts enemy position to all squadmates in range.
        /// </summary>
        public void EchoSpottedEnemyToSquad(Vector3 enemyPos)
        {
            if (_bot.BotsGroup == null)
                return;

            for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
            {
                var mate = _bot.BotsGroup.Member(i);
                if (!IsValidSquadmate(mate)) continue;

                var mateCache = BotCacheUtility.GetCache(mate);
                mateCache?.TacticalMemory?.RecordEnemyPosition(enemyPos);
            }
        }

        #endregion

        #region Helpers

        private bool IsValidSquadmate(BotOwner? mate)
        {
            return mate != null &&
                   mate != _bot &&
                   !mate.IsDead &&
                   Vector3.Distance(mate.Position, _bot.Position) < MaxEchoRange;
        }

        private bool CanAcceptEcho(BotComponentCache cache)
        {
            if (cache.IsBlinded || cache.PanicHandler?.IsPanicking == true)
                return false;

            var profile = cache.AIRefactoredBotOwner?.PersonalityProfile;
            return profile != null && profile.Caution >= 0.2f;
        }

        #endregion
    }
}
