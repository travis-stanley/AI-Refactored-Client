#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using UnityEngine;

namespace AIRefactored.AI.Medical
{
    /// <summary>
    /// Manages bot injuries, healing behavior, and surgical procedures on destroyed limbs.
    /// Prioritizes realistic timing, cover safety, and cooldown between medical actions.
    /// </summary>
    public class BotInjurySystem
    {
        #region Constants

        private const float HealCooldown = 6f;

        #endregion

        #region Fields

        private readonly BotComponentCache _cache;
        private float _lastHitTime;
        private float _nextHealTime;
        private EBodyPart? _injuredLimb;
        private bool _hasBlackLimb;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Initialization

        public BotInjurySystem(BotComponentCache cache)
        {
            _cache = cache ?? throw new System.ArgumentNullException(nameof(cache));
        }

        #endregion

        #region Damage Tracking

        /// <summary>
        /// Called when the bot is damaged. Tracks cooldown and injury flags.
        /// </summary>
        public void OnHit(EBodyPart part, float damage)
        {
            _lastHitTime = Time.time;
            _nextHealTime = _lastHitTime + HealCooldown;
            _injuredLimb = part;

            var health = _cache.Bot?.GetPlayer?.HealthController;
            _hasBlackLimb = health != null && health.IsBodyPartDestroyed(part);
        }

        #endregion

        #region Public Evaluation

        public void Tick(float time)
        {
            if (ShouldHeal(time))
                TryUseMedicine();
        }

        public bool ShouldHeal()
        {
            return ShouldHeal(Time.time);
        }

        public bool ShouldHeal(float time)
        {
            if (_injuredLimb == null || !_hasBlackLimb)
                return false;

            if (time < _nextHealTime)
                return false;

            if (_cache.PanicHandler?.IsPanicking == true)
                return false;

            if (_cache.Combat?.IsInCombatState() == true)
                return false;

            return true;
        }

        #endregion

        #region Healing Execution

        private void TryUseMedicine()
        {
            var bot = _cache.Bot;
            if (bot == null || bot.IsDead)
                return;

            var player = bot.GetPlayer;
            if (player == null || player.HealthController == null || _injuredLimb == null)
                return;

            var health = player.HealthController;

            if (!health.IsBodyPartDestroyed(_injuredLimb.Value))
                return;

            var surgical = bot.Medecine?.SurgicalKit;
            if (surgical == null || !surgical.HaveWork || !surgical.ShallStartUse())
                return;

            // === Realism Actions ===
            bot.Sprint(false);
            bot.WeaponManager?.Selector?.TakePrevWeapon();
            bot.BotTalk?.Say(EPhraseTrigger.StartHeal, false, null);

            // === Surgery ===
            surgical.ApplyToCurrentPart();
            Reset();
        }

        #endregion

        #region Internal State

        public void Reset()
        {
            _injuredLimb = null;
            _hasBlackLimb = false;
        }

        #endregion
    }
}
