#nullable enable

using AIRefactored.AI.Core;
using UnityEngine;

namespace AIRefactored.AI.Medical
{
    /// <summary>
    /// Tracks injury state, cooldown, and realistic safety before using medicine.
    /// </summary>
    public class BotInjurySystem
    {
        private readonly BotComponentCache _cache;

        private float _lastHitTime;
        private float _nextHealTime;
        private EBodyPart? _injuredLimb;
        private bool _hasBlackLimb;

        private const float HealCooldown = 6f;

        public BotInjurySystem(BotComponentCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Called when the bot is hit. Flags healing cooldown and limb damage.
        /// </summary>
        public void OnHit(EBodyPart part, float damage)
        {
            _lastHitTime = Time.time;
            _nextHealTime = Time.time + HealCooldown;

            var health = _cache.Bot?.GetPlayer?.HealthController;
            if (health == null)
                return;

            _injuredLimb = part;
            _hasBlackLimb = health.IsBodyPartDestroyed(part);
        }

        /// <summary>
        /// External Tick from BotBrain.
        /// </summary>
        public void Tick(float time)
        {
            if (ShouldHeal(time))
            {
                TryUseMedicine();
            }
        }

        /// <summary>
        /// Checks if the bot should initiate healing at current time.
        /// </summary>
        public bool ShouldHeal() => ShouldHeal(Time.time);

        /// <summary>
        /// Determines whether the bot should begin healing.
        /// </summary>
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

        /// <summary>
        /// Attempts to trigger surgery if the bot has a valid black limb.
        /// </summary>
        private void TryUseMedicine()
        {
            var bot = _cache.Bot;
            if (bot == null || bot.IsDead || _injuredLimb == null)
                return;

            var health = bot.GetPlayer?.HealthController;
            if (health == null || !health.IsBodyPartDestroyed(_injuredLimb.Value))
                return;

            var surgical = bot.Medecine?.SurgicalKit;
            if (surgical == null || !surgical.HaveWork || !surgical.ShallStartUse())
                return;

            bot.Sprint(false);
            bot.WeaponManager?.Selector?.TakePrevWeapon();
            bot.BotTalk?.Say(EPhraseTrigger.StartHeal, false, null);

            surgical.ApplyToCurrentPart();
            Reset();
        }

        /// <summary>
        /// Resets internal state after healing finishes.
        /// </summary>
        public void Reset()
        {
            _injuredLimb = null;
            _hasBlackLimb = false;
        }
    }
}
