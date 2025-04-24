#nullable enable

using AIRefactored.AI.Components;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Movement;
using AIRefactored.AI.Optimization;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat.States
{
    /// <summary>
    /// Handles bot behavior while in the Patrol combat state.
    /// Evaluates sound cues, injuries, panic, suppression, and squad loss to trigger fallback.
    /// Periodically patrols between hotspots.
    /// </summary>
    public sealed class PatrolHandler
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotOwner _bot;

        private readonly float _minStateDuration;
        private readonly float _switchCooldownBase;
        private float _nextSwitchTime;

        private const float InvestigateSoundDelay = 3f;
        private const float DeadAllyRadius = 10f;
        private const float PanicThreshold = 0.25f;

        #endregion

        #region Constructor

        public PatrolHandler(BotComponentCache cache, float minStateDuration = 1.25f, float switchCooldownBase = 12f)
        {
            _cache = cache;
            _bot = cache.Bot!;
            _minStateDuration = minStateDuration;
            _switchCooldownBase = switchCooldownBase;
        }

        #endregion

        #region Evaluation

        /// <summary>
        /// Always active when no other combat layer is active.
        /// </summary>
        public bool ShallUseNow() => true;

        /// <summary>
        /// Checks if caution + recent sound should trigger Investigate.
        /// </summary>
        public bool ShouldTransitionToInvestigate(float time)
        {
            return _cache.LastHeardTime + InvestigateSoundDelay > time &&
                   _cache.AIRefactoredBotOwner?.PersonalityProfile?.Caution > 0.35f &&
                   time - _cache.Combat!.LastStateChangeTime > _minStateDuration;
        }

        #endregion

        #region Main Logic

        /// <summary>
        /// Drives patrol pathing, fallback checks, and ambient VO.
        /// </summary>
        public void Tick(float time)
        {
            if (ShouldTriggerFallback())
            {
                Vector3 fallback = HybridFallbackResolver.GetBestRetreatPoint(_bot, _bot.LookDirection) ?? _bot.Position;
                _cache.Combat?.TriggerFallback(fallback);
                return;
            }

            if (time >= _nextSwitchTime)
            {
                Vector3 baseTarget = HotspotRegistry.GetRandomHotspot().Position;
                Vector3 target = _cache.Pathing?.ApplyOffsetTo(baseTarget) ?? baseTarget;

                BotMovementHelper.SmoothMoveTo(_bot, target);
                BotCoverHelper.TrySetStanceFromNearbyCover(_cache, target);

                _nextSwitchTime = time + Random.Range(_switchCooldownBase, _switchCooldownBase + 18f);

                if (Random.value < 0.25f)
                    _bot.BotTalk?.TrySay(EPhraseTrigger.GoForward);
            }
        }

        #endregion

        #region Fallback Conditions

        /// <summary>
        /// Checks panic, injuries, suppression, or nearby dead squadmates to trigger fallback.
        /// </summary>
        private bool ShouldTriggerFallback()
        {
            float composure = _cache.PanicHandler?.GetComposureLevel() ?? 1f;
            if (composure < PanicThreshold)
                return true;

            if (_cache.InjurySystem?.ShouldHeal() == true)
                return true;

            if (_cache.Suppression?.IsSuppressed() == true)
                return true;

            if (_bot.BotsGroup != null)
            {
                for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
                {
                    var mate = _bot.BotsGroup.Member(i);
                    if (mate != null && mate != _bot && mate.IsDead &&
                        Vector3.Distance(_bot.Position, mate.Position) < DeadAllyRadius)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion
    }
}
