﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

#nullable enable

namespace AIRefactored.AI.Combat
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Medical;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Controls bot healing behavior using first aid, surgery, or stimulators.
    /// Supports healing squadmates using internal EFT BotHealAnotherTarget logic.
    /// </summary>
    public sealed class BotMedicLogic
    {
        #region Constants

        private const float HealCheckInterval = 1.5f;
        private const float HealSquadRange = 4f;

        #endregion

        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotInjurySystem _injurySystem;
        private BotMedecine? _med;
        private bool _isHealing;
        private float _nextHealCheck;

        #endregion

        #region Constructor

        public BotMedicLogic(BotComponentCache cache, BotInjurySystem injurySystem)
        {
            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            if (injurySystem == null)
            {
                throw new ArgumentNullException(nameof(injurySystem));
            }

            this._cache = cache;
            this._injurySystem = injurySystem;
            this._nextHealCheck = Time.time;

            BotOwner? bot = this._cache.Bot;
            if (bot != null)
            {
                if (bot.HealAnotherTarget == null)
                {
                    bot.HealAnotherTarget = new BotHealAnotherTarget(bot);
                    bot.HealAnotherTarget.OnHealAsked += this.OnHealAsked;
                }

                if (bot.HealingBySomebody == null)
                {
                    bot.HealingBySomebody = new BotHealingBySomebody(bot);
                }
            }
        }

        #endregion

        #region Public Methods

        public void Reset()
        {
            this._isHealing = false;
            this._injurySystem.Reset();
            this.UnsubscribeFromFirstAid();
        }

        public void Tick(float time)
        {
            BotOwner? bot = this._cache.Bot;
            if (bot == null || bot.IsDead || bot.GetPlayer == null || !bot.GetPlayer.IsAI)
            {
                return;
            }

            if (this._cache.PanicHandler?.IsPanicking == true ||
                this._isHealing ||
                time < this._nextHealCheck ||
                (bot.HealAnotherTarget != null && bot.HealAnotherTarget.IsInProcess))
            {
                return;
            }

            this._injurySystem.Tick(time);

            if (this._med == null)
            {
                this._med = bot.Medecine;
                if (this._med == null)
                {
                    return;
                }
            }

            this._nextHealCheck = time + HealCheckInterval;

            if (this.TryHealSquadmate(bot))
            {
                return;
            }

            this.TrySelfHeal(bot);
        }

        #endregion

        #region Private Methods

        private void BeginHeal()
        {
            this._isHealing = true;
            this.UnsubscribeFromFirstAid();
        }

        private void OnHealAsked(IPlayer target)
        {
            this._isHealing = true;
            this.TrySay(EPhraseTrigger.StartHeal);
        }

        private void OnHealComplete(BotOwner _)
        {
            this._isHealing = false;
            this._injurySystem.Reset();
            this.UnsubscribeFromFirstAid();
        }

        private void OnStimComplete(bool success)
        {
            this._isHealing = false;
        }

        private void OnSurgeryComplete()
        {
            this._isHealing = false;
            this._med?.FirstAid?.CheckParts();
        }

        private bool TryHealSquadmate(BotOwner bot)
        {
            if (bot.BotsGroup == null || bot.HealAnotherTarget == null)
            {
                return false;
            }

            Player? selfPlayer = EFTPlayerUtil.ResolvePlayer(bot);
            if (!EFTPlayerUtil.IsValidGroupPlayer(selfPlayer))
            {
                return false;
            }

            Vector3 botPos = EFTPlayerUtil.GetPosition(selfPlayer);

            for (int i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                BotOwner? mate = bot.BotsGroup.Member(i);
                if (mate == null || mate.IsDead || mate == bot)
                {
                    continue;
                }

                Player? matePlayer = EFTPlayerUtil.ResolvePlayer(mate);
                if (!EFTPlayerUtil.IsValidGroupPlayer(matePlayer))
                {
                    continue;
                }

                Vector3 matePos = EFTPlayerUtil.GetPosition(matePlayer);

                if (Vector3.Distance(botPos, matePos) <= HealSquadRange)
                {
                    EFT.IPlayer? iTarget = EFTPlayerUtil.AsSafeIPlayer(matePlayer);
                    if (iTarget != null)
                    {
                        bot.HealAnotherTarget.HealAsk(iTarget);
                        return true;
                    }
                }
            }

            return false;
        }

        private void TrySelfHeal(BotOwner bot)
        {
            var firstAid = this._med?.FirstAid;
            var surgery = this._med?.SurgicalKit;
            var stim = this._med?.Stimulators;

            if (firstAid != null && firstAid.ShallStartUse())
            {
                this.BeginHeal();
                firstAid.OnEndApply += this.OnHealComplete;
                firstAid.TryApplyToCurrentPart();
                this.TrySay(EPhraseTrigger.StartHeal);
                return;
            }

            if (surgery != null && surgery.ShallStartUse())
            {
                this.BeginHeal();
                surgery.ApplyToCurrentPart(this.OnSurgeryComplete);
                this.TrySay(EPhraseTrigger.StartHeal);
                return;
            }

            if (stim != null && stim.HaveSmt && stim.CanUseNow())
            {
                this.BeginHeal();
                stim.StartApplyToTarget(this.OnStimComplete);
            }
        }

        private void TrySay(EPhraseTrigger trigger)
        {
            BotOwner? bot = this._cache.Bot;
            if (!FikaHeadlessDetector.IsHeadless && bot != null && bot.BotTalk != null)
            {
                bot.BotTalk.TrySay(trigger);
            }
        }

        private void UnsubscribeFromFirstAid()
        {
            if (this._med != null && this._med.FirstAid != null)
            {
                this._med.FirstAid.OnEndApply -= this.OnHealComplete;
            }
        }

        #endregion
    }
}
