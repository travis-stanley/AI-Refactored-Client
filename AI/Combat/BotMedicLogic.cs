#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Medical;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Executes healing and buffing logic for bots based on their injury state.
    /// Supports usage of first aid, surgical kits, and stimulators.
    /// Mimics realistic player medical behavior with cooldowns and voice triggers.
    /// </summary>
    public class BotMedicLogic
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotInjurySystem _injurySystem;

        private BotMedecine? _med;
        private bool _isHealing;
        private float _nextHealCheck;

        private const float HealCheckInterval = 1.5f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new medic logic handler for the given bot.
        /// </summary>
        /// <param name="cache">Component cache reference.</param>
        /// <param name="injurySystem">Linked injury tracking system.</param>
        public BotMedicLogic(BotComponentCache cache, BotInjurySystem injurySystem)
        {
            _cache = cache;
            _injurySystem = injurySystem;
        }

        #endregion

        #region Tick Logic

        /// <summary>
        /// Called per tick to process healing behavior based on injuries and cooldowns.
        /// </summary>
        /// <param name="time">Current world time.</param>
        public void Tick(float time)
        {
            var bot = _cache.Bot;
            if (bot == null || bot.IsDead || !bot.IsAI)
                return;

            _med ??= bot.Medecine;
            if (_med == null)
                return;

            _injurySystem.Tick(time);

            if (_isHealing || time < _nextHealCheck || _cache.PanicHandler?.IsPanicking == true)
                return;

            _nextHealCheck = time + HealCheckInterval;
            TryHealOrBuff();
        }

        #endregion

        #region Healing Logic

        /// <summary>
        /// Attempts to initiate a healing or buffing action if appropriate.
        /// </summary>
        private void TryHealOrBuff()
        {
            if (_med == null)
                return;

            var firstAid = _med.FirstAid;
            var surgery = _med.SurgicalKit;
            var stim = _med.Stimulators;

            // === Priority 1: First Aid ===
            if (firstAid != null && firstAid.ShallStartUse())
            {
                _isHealing = true;
                firstAid.OnEndApply += OnHealComplete;
                firstAid.TryApplyToCurrentPart();
                _cache.Bot?.BotTalk?.TrySay(EPhraseTrigger.StartHeal);
                return;
            }

            // === Priority 2: Surgical Kit ===
            if (surgery != null && surgery.ShallStartUse())
            {
                _isHealing = true;
                surgery.ApplyToCurrentPart(() =>
                {
                    _isHealing = false;
                    firstAid?.CheckParts();
                });
                _cache.Bot?.BotTalk?.TrySay(EPhraseTrigger.StartHeal);
                return;
            }

            // === Priority 3: Stimulators ===
            if (stim != null && stim.HaveSmt && stim.CanUseNow())
            {
                _isHealing = true;
                stim.StartApplyToTarget(_ => _isHealing = false);
            }
        }

        /// <summary>
        /// Called when healing finishes via first aid.
        /// </summary>
        /// <param name="bot">The bot that completed healing.</param>
        private void OnHealComplete(BotOwner bot)
        {
            _isHealing = false;
            _injurySystem.Reset();

            if (_med?.FirstAid != null)
            {
                _med.FirstAid.OnEndApply -= OnHealComplete;
            }
        }

        /// <summary>
        /// Resets healing state, including active timers and callbacks.
        /// </summary>
        public void Reset()
        {
            _isHealing = false;
            _injurySystem.Reset();

            if (_med?.FirstAid != null)
            {
                _med.FirstAid.OnEndApply -= OnHealComplete;
            }
        }

        #endregion
    }
}
