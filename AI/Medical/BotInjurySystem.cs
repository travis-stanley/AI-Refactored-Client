#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using UnityEngine;

namespace AIRefactored.AI.Medical
{
    /// <summary>
    /// Manages bot injury state and healing behavior.
    /// Tracks cooldowns, determines safe moments for healing,
    /// and triggers surgical procedures on destroyed limbs.
    /// </summary>
    public class BotInjurySystem
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private float _lastHitTime;
        private float _nextHealTime;
        private EBodyPart? _injuredLimb;
        private bool _hasBlackLimb;

        private const float HealCooldown = 6f;
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Creates a new injury tracking system for the bot.
        /// </summary>
        /// <param name="cache">The bot’s component cache.</param>
        public BotInjurySystem(BotComponentCache cache)
        {
            _cache = cache;
        }

        #endregion

        #region Event Hooks

        /// <summary>
        /// Called when the bot is hit. Flags limb as injured and begins cooldown.
        /// </summary>
        /// <param name="part">The limb that was hit.</param>
        /// <param name="damage">The damage value.</param>
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

        #endregion

        #region Tick & Decision Logic

        /// <summary>
        /// Called every frame by the bot brain.
        /// </summary>
        /// <param name="time">Current game time.</param>
        public void Tick(float time)
        {
            if (ShouldHeal(time))
                TryUseMedicine();
        }

        /// <summary>
        /// Whether the bot should attempt healing at the current time.
        /// </summary>
        /// <returns>True if healing should begin.</returns>
        public bool ShouldHeal() => ShouldHeal(Time.time);

        /// <summary>
        /// Evaluates if the bot is in a safe and appropriate condition to begin healing.
        /// </summary>
        /// <param name="time">Current game time.</param>
        /// <returns>True if healing should begin.</returns>
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

        #region Healing Logic

        /// <summary>
        /// Attempts to begin surgical healing if possible.
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
        /// Clears internal injury tracking state after healing completes.
        /// </summary>
        public void Reset()
        {
            _injuredLimb = null;
            _hasBlackLimb = false;
        }

        #endregion
    }
}
