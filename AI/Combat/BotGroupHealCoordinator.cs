#nullable enable

using System;
using System.Collections.Generic;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.Core;
using EFT;
using EFT.HealthSystem;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Coordinates healing support across squadmates. Detects injured allies and applies
    /// realistic support actions: stim packs, aid drops, or voiced reassurances.
    /// </summary>
    public sealed class BotGroupHealCoordinator
    {
        #region Constants

        private const float HealCheckInterval = 3.5f;
        private const float HealTriggerRange = 10f;
        private const float HealthThreshold = 0.6f;

        #endregion

        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotOwner _bot;

        private float _nextCheckTime;

        #endregion

        #region Constructor

        public BotGroupHealCoordinator(BotComponentCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _bot = cache.Bot ?? throw new ArgumentNullException(nameof(cache.Bot));
        }

        #endregion

        #region Tick

        /// <summary>
        /// Periodically scans squadmates and triggers healing logic for nearby allies.
        /// </summary>
        public void Tick(float time)
        {
            if (_bot.IsDead || _bot.BotsGroup == null || time < _nextCheckTime)
                return;

            _nextCheckTime = time + HealCheckInterval;

            for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
            {
                var mate = _bot.BotsGroup.Member(i);
                if (!IsValidMate(mate))
                    continue;

                var health = mate.GetPlayer?.HealthController;
                if (health == null || !health.IsAlive)
                    continue;

                if (NeedsHealing(health))
                {
                    // Ask Tarkov's healer logic to initiate squad healing
                    if (_cache.SquadHealer != null && !_cache.SquadHealer.IsInProcess)
                    {
                        _cache.SquadHealer.HealAsk(mate.GetPlayer!);
                        TrySaySupport(EPhraseTrigger.Cooperation);
                        return;
                    }

                    // Drop stim if squad healer logic is not available or fails
                    TryDropStimForMate(mate);
                }
            }
        }

        #endregion

        #region Validation

        private bool IsValidMate(BotOwner? mate)
        {
            return mate != null && mate != _bot && !mate.IsDead &&
                   Vector3.Distance(_bot.Position, mate.Position) <= HealTriggerRange;
        }

        private bool NeedsHealing(IHealthController health)
        {
            foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
            {
                var hp = health.GetBodyPartHealth(part);
                if (hp.Current < hp.Maximum * HealthThreshold)
                    return true;
            }

            return false;
        }

        #endregion

        #region Stim Support Logic

        private void TryDropStimForMate(BotOwner mate)
        {
            var stim = _bot.Medecine?.Stimulators;
            if (stim == null || !stim.HaveSmt || !stim.CanUseNow())
                return;

            stim.StartApplyToTarget(success =>
            {
                if (success)
                    TrySaySupport(EPhraseTrigger.NeedHelp);
            });
        }

        private void TrySaySupport(EPhraseTrigger phrase)
        {
            if (!FikaHeadlessDetector.IsHeadless)
                _bot.BotTalk?.TrySay(phrase);
        }

        #endregion
    }
}
