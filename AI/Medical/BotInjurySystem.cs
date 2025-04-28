#nullable enable

namespace AIRefactored.AI.Medical
{
    using System;

    using AIRefactored.AI.Core;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using UnityEngine;

    /// <summary>
    ///     Manages bot injuries, healing behavior, and surgical procedures on destroyed limbs.
    ///     Prioritizes realistic timing, cover safety, and cooldown between medical actions.
    /// </summary>
    public sealed class BotInjurySystem
    {
        private const float HealCooldown = 6f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private readonly BotComponentCache _cache;

        private bool _hasBlackLimb;

        private EBodyPart? _injuredLimb;

        private float _lastHitTime;

        private float _nextHealTime;

        public BotInjurySystem(BotComponentCache cache)
        {
            this._cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        ///     Called when the bot is damaged. Tracks cooldown and injury flags.
        /// </summary>
        public void OnHit(EBodyPart part, float damage)
        {
            this._lastHitTime = Time.time;
            this._nextHealTime = this._lastHitTime + HealCooldown;
            this._injuredLimb = part;

            var health = this._cache.Bot?.GetPlayer?.HealthController;
            this._hasBlackLimb = health != null && health.IsBodyPartDestroyed(part);
        }

        public void Reset()
        {
            this._injuredLimb = null;
            this._hasBlackLimb = false;
        }

        public bool ShouldHeal()
        {
            return this.ShouldHeal(Time.time);
        }

        public bool ShouldHeal(float time)
        {
            if (this._injuredLimb == null || !this._hasBlackLimb)
                return false;

            if (time < this._nextHealTime)
                return false;

            if (this._cache.PanicHandler?.IsPanicking == true)
                return false;

            if (this._cache.Combat?.IsInCombatState() == true)
                return false;

            return true;
        }

        public void Tick(float time)
        {
            if (this.ShouldHeal(time)) this.TryUseMedicine();
        }

        private void TryUseMedicine()
        {
            var bot = this._cache.Bot;
            if (bot == null || bot.IsDead)
                return;

            var player = bot.GetPlayer;
            var health = player?.HealthController;
            if (player == null || health == null || this._injuredLimb == null)
                return;

            if (!health.IsBodyPartDestroyed(this._injuredLimb.Value))
                return;

            var surgical = bot.Medecine?.SurgicalKit;
            if (surgical == null || !surgical.HaveWork || !surgical.ShallStartUse())
                return;

            // === Realism Actions ===
            bot.Sprint(false);
            bot.WeaponManager?.Selector?.TakePrevWeapon();
            bot.BotTalk?.Say(EPhraseTrigger.StartHeal);

            // === Surgery ===
            surgical.ApplyToCurrentPart();
            this.Reset();

            Logger.LogDebug(
                $"[BotInjurySystem] 🛠 {bot.Profile?.Info?.Nickname ?? "Unknown"} applied surgery to {this._injuredLimb.Value}");
        }
    }
}