#nullable enable

using EFT;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Medical;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Executes healing and buffing actions for bots when injured or weakened.
    /// Uses first aid, surgical kits, painkillers, and stims just like real players.
    /// </summary>
    public class BotMedicLogic
    {
        private readonly BotComponentCache _cache;
        private readonly BotInjurySystem _injurySystem;

        private BotMedecine? _med;
        private bool _isHealing;
        private float _nextHealCheck;

        private const float HealCheckInterval = 1.5f;

        public BotMedicLogic(BotComponentCache cache, BotInjurySystem injurySystem)
        {
            _cache = cache;
            _injurySystem = injurySystem;
        }

        public void Tick(float time)
        {
            if (_cache.Bot == null || _cache.Bot.IsDead || !_cache.Bot.IsAI)
                return;

            _med ??= _cache.Bot.Medecine;
            if (_med == null)
                return;

            _injurySystem.Update();

            if (_isHealing)
                return;

            if (time < _nextHealCheck)
                return;

            _nextHealCheck = time + HealCheckInterval;

            TryHealOrBuff();
        }

        private void TryHealOrBuff()
        {
            if (_med == null || _cache.Bot == null || _cache.Panic?.IsPanicking == true)
                return;

            // === Priority 1: First Aid ===
            if (_med.FirstAid.ShallStartUse())
            {
                _isHealing = true;
                _med.FirstAid.OnEndApply += OnHealComplete;
                _med.FirstAid.TryApplyToCurrentPart();
                _cache.Bot.BotTalk?.TrySay(EPhraseTrigger.StartHeal);
                return;
            }

            // === Priority 2: Surgical Kit ===
            if (_med.SurgicalKit.ShallStartUse())
            {
                _isHealing = true;
                _med.SurgicalKit.ApplyToCurrentPart(() =>
                {
                    _isHealing = false;
                    _med.FirstAid.CheckParts(); // recheck follow-up meds
                });
                _cache.Bot.BotTalk?.TrySay(EPhraseTrigger.StartHeal);
                return;
            }

            // === Priority 3: Stimulants (Painkillers, Buffs) ===
            if (_med.Stimulators.HaveSmt && _med.Stimulators.CanUseNow())
            {
                _isHealing = true;
                _med.Stimulators.StartApplyToTarget(success =>
                {
                    _isHealing = false;
                });
                return;
            }
        }

        private void OnHealComplete(BotOwner bot)
        {
            _isHealing = false;
            _injurySystem.Reset();

            if (_med != null)
            {
                _med.FirstAid.OnEndApply -= OnHealComplete;
            }
        }

        public void Reset()
        {
            _isHealing = false;
            _injurySystem.Reset();
        }
    }
}
