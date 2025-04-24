#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Medical;
using AIRefactored.Core;
using EFT;
using System;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Controls bot healing behavior using first aid, surgery, or stimulators.
    /// Supports healing squadmates using internal EFT BotHealAnotherTarget logic.
    /// </summary>
    public sealed class BotMedicLogic
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotInjurySystem _injurySystem;

        private BotMedecine? _med;
        private bool _isHealing;
        private float _nextHealCheck;

        private const float HealCheckInterval = 1.5f;
        private const float HealSquadRange = 4f;

        #endregion

        #region Constructor

        public BotMedicLogic(BotComponentCache cache, BotInjurySystem injurySystem)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _injurySystem = injurySystem ?? throw new ArgumentNullException(nameof(injurySystem));
            _nextHealCheck = Time.time;

            var bot = _cache.Bot;
            if (bot != null)
            {
                bot.HealAnotherTarget ??= new BotHealAnotherTarget(bot);
                bot.HealAnotherTarget.OnHealAsked += OnHealAsked;
                bot.HealingBySomebody ??= new BotHealingBySomebody(bot);
            }
        }

        #endregion

        #region Tick Loop

        public void Tick(float time)
        {
            var bot = _cache.Bot;
            if (bot == null || bot.IsDead || bot.GetPlayer == null || !bot.GetPlayer.IsAI)
                return;

            if (_cache.PanicHandler?.IsPanicking == true ||
                bot.HealingBySomebody?.IsInProcess == true ||
                bot.HealAnotherTarget?.IsInProcess == true ||
                _isHealing || time < _nextHealCheck)
                return;

            _injurySystem.Tick(time);
            _med ??= bot.Medecine;
            if (_med == null)
                return;

            _nextHealCheck = time + HealCheckInterval;

            if (TryHealSquadmate(bot))
                return;

            TrySelfHeal(bot);
        }

        #endregion

        #region Squadmate Heal Logic

        private bool TryHealSquadmate(BotOwner bot)
        {
            if (bot.BotsGroup == null || bot.HealAnotherTarget == null)
                return false;

            for (int i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                var mate = bot.BotsGroup.Member(i);
                if (mate == null || mate == bot || mate.IsDead)
                    continue;

                var mateBot = mate.GetPlayer;
                if (mateBot?.HealthController == null || !mateBot.IsAI)
                    continue;

                if (Vector3.Distance(mate.Position, bot.Position) < HealSquadRange)
                {
                    if (!bot.HealAnotherTarget.IsInProcess && mateBot.HealthController.IsAlive)
                    {
                        bot.HealAnotherTarget.HealAsk(mateBot);
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnHealAsked(IPlayer target)
        {
            if (_cache.GroupComms != null && !_cache.GroupComms.IsMuted)
                _cache.GroupComms.Say(EPhraseTrigger.StartHeal);

            _isHealing = true;
        }

        #endregion

        #region Self Heal Logic

        private void TrySelfHeal(BotOwner bot)
        {
            var firstAid = _med?.FirstAid;
            var surgery = _med?.SurgicalKit;
            var stim = _med?.Stimulators;

            if (firstAid != null && firstAid.ShallStartUse())
            {
                BeginHeal();
                firstAid.OnEndApply += OnHealComplete;
                firstAid.TryApplyToCurrentPart();
                Say(EPhraseTrigger.StartHeal);
                return;
            }

            if (surgery != null && surgery.ShallStartUse())
            {
                BeginHeal();
                surgery.ApplyToCurrentPart(OnSurgeryComplete);
                Say(EPhraseTrigger.StartHeal);
                return;
            }

            if (stim != null && stim.HaveSmt && stim.CanUseNow())
            {
                BeginHeal();
                stim.StartApplyToTarget(OnStimComplete);
            }
        }

        private void BeginHeal()
        {
            _isHealing = true;
            UnsubscribeFromFirstAid();
        }

        private void OnHealComplete(BotOwner _)
        {
            _isHealing = false;
            _injurySystem.Reset();
            UnsubscribeFromFirstAid();
        }

        private void OnSurgeryComplete()
        {
            _isHealing = false;
            _med?.FirstAid?.CheckParts();
        }

        private void OnStimComplete(bool success)
        {
            _isHealing = false;
        }

        #endregion

        #region Cleanup + Voice

        public void Reset()
        {
            _isHealing = false;
            _injurySystem.Reset();
            UnsubscribeFromFirstAid();
        }

        private void UnsubscribeFromFirstAid()
        {
            if (_med?.FirstAid != null)
                _med.FirstAid.OnEndApply -= OnHealComplete;
        }

        private void Say(EPhraseTrigger trigger)
        {
            if (!FikaHeadlessDetector.IsHeadless)
                _cache.Bot?.BotTalk?.TrySay(trigger);
        }

        #endregion
    }
}
