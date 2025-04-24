#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Controls bot fire cadence, accuracy, fire mode decisions, and fallback.
    /// Adjusts dynamically based on distance, suppression, personality, and ammo.
    /// Also handles aiming direction realism and sky-snap protection.
    /// </summary>
    public sealed class BotFireLogic
    {
        #region Constants

        private static readonly Dictionary<string, float> WeaponTypeRanges = new(StringComparer.OrdinalIgnoreCase)
        {
            { "sniper",    180f },
            { "marksman",  150f },
            { "rifle",     120f },
            { "assault",   100f },
            { "smg",        75f },
            { "shotgun",    50f },
            { "pistol",     35f }
        };

        private static readonly EBodyPart[] AllBodyParts = (EBodyPart[])Enum.GetValues(typeof(EBodyPart));
        private const float MaxAimPitch = 70f;

        #endregion

        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;

        private float _nextDecisionTime;
        private float _lastLookAroundTime;
        private Vector3 _idleLookDir = Vector3.forward;

        #endregion

        #region Constructor

        public BotFireLogic(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        #endregion

        #region Tick

        public void Tick(float time)
        {
            if (_bot == null || _bot.IsDead || !_bot.IsAI || _bot.Memory == null)
                return;

            var weaponMgr = _bot.WeaponManager;
            var shootData = _bot.ShootData;
            var weaponInfo = weaponMgr?._currentWeaponInfo;
            var weapon = weaponInfo?.weapon;
            var settings = _bot.Settings?.FileSettings?.Core as GClass592;
            var profile = BotRegistry.Get(_bot.ProfileId);

            if (weaponMgr == null || shootData == null || weaponInfo == null || weapon == null || settings == null || profile == null)
                return;

            var target = _cache.ThreatSelector?.CurrentTarget ?? _bot.Memory.GoalEnemy?.Person;

            Vector3 aimPosition = GetValidatedAimPosition(target, time);
            Vector3 direction = aimPosition - _bot.Position;

            if (direction != Vector3.zero)
            {
                Quaternion flatLook = Quaternion.LookRotation(direction);
                Vector3 flatEuler = flatLook.eulerAngles;
                flatEuler.x = Mathf.Clamp(flatEuler.x > 180f ? flatEuler.x - 360f : flatEuler.x, -MaxAimPitch, MaxAimPitch);
                _bot.AimingManager?.CurrentAiming?.SetTarget(Quaternion.Euler(flatEuler) * Vector3.forward);
            }

            if (target?.HealthController?.IsAlive != true)
                return;

            float dist = Vector3.Distance(_bot.Position, aimPosition);
            float weaponRange = EstimateWeaponRange(weapon);
            float maxRange = Mathf.Min(profile.EngagementRange, weaponRange, 200f);

            if (_bot.Memory.IsUnderFire && GetHealthRatio() <= profile.RetreatThreshold)
            {
                TriggerFallback();
                return;
            }

            if (dist > maxRange)
            {
                if (UnityEngine.Random.value < profile.ChaosFactor)
                    BotMovementHelper.SmoothMoveTo(_bot, aimPosition, false, profile.Cohesion);
                return;
            }

            if (time < _nextDecisionTime)
                return;

            _nextDecisionTime = time + GetBurstCadence(profile);

            if (weaponInfo.BulletCount <= 0 && !weaponInfo.CheckHaveAmmoForReload())
            {
                weaponMgr.Selector.TryChangeWeaponCauseNoAmmo();
                weaponMgr.Melee.Activate();
                return;
            }

            ApplyFireMode(weaponInfo, weapon, dist, profile, settings);

            if (weaponMgr.IsWeaponReady)
            {
                shootData.Shoot();
                _cache.LastShotTracker?.RegisterShot(target);
            }
        }

        #endregion

        #region Fire Mode Handling

        private void ApplyFireMode(BotWeaponInfo info, Weapon weapon, float distance, BotPersonalityProfile profile, GClass592 core)
        {
            if (distance <= 40f)
            {
                SetFireMode(info, Weapon.EFireMode.fullauto);
                RecoverAccuracy(core);
            }
            else if (distance <= 100f && SupportsFireMode(weapon, Weapon.EFireMode.burst))
            {
                SetFireMode(info, Weapon.EFireMode.burst);
                ApplyScatter(core, true, profile);
            }
            else
            {
                SetFireMode(info, Weapon.EFireMode.single);
                ApplyScatter(core, true, profile);
            }
        }

        private void SetFireMode(BotWeaponInfo info, Weapon.EFireMode mode)
        {
            if (info.weapon.SelectedFireMode != mode)
                info.ChangeFireMode(mode);
        }

        private bool SupportsFireMode(Weapon weapon, Weapon.EFireMode mode)
        {
            foreach (var fireMode in weapon.WeapFireType)
                if (fireMode == mode) return true;
            return false;
        }

        #endregion

        #region Accuracy + Cadence

        private void ApplyScatter(GClass592 core, bool underFire, BotPersonalityProfile profile)
        {
            float composure = _cache.PanicHandler?.GetComposureLevel() ?? 1f;
            float scatterPenalty = underFire ? (1f - profile.AccuracyUnderFire) * (1f - composure) : 0f;
            float scatterFactor = 1.1f + scatterPenalty;

            core.ScatteringPerMeter = Mathf.Clamp(core.ScatteringPerMeter * scatterFactor, 0.6f, 3.5f);
        }

        private void RecoverAccuracy(GClass592 core)
        {
            core.ScatteringPerMeter *= 0.95f;
            core.ScatteringPerMeter = Mathf.Clamp(core.ScatteringPerMeter, 0.4f, 3.0f);
        }

        private float GetBurstCadence(BotPersonalityProfile profile)
        {
            float baseDelay = Mathf.Lerp(0.75f, 0.25f, profile.AggressionLevel);
            float reactionDelay = Mathf.Lerp(0.15f, 0.35f, 1f - profile.ReactionTime);
            float chaosOffset = UnityEngine.Random.Range(-0.08f, 0.2f) * profile.ChaosFactor;

            return Mathf.Clamp(baseDelay + reactionDelay + chaosOffset, 0.15f, 1.1f);
        }

        #endregion

        #region Fallback Logic

        private float GetHealthRatio()
        {
            var health = _bot.GetPlayer?.HealthController;
            if (health == null) return 1f;

            float current = 0f;
            float max = 0f;

            for (int i = 0; i < AllBodyParts.Length; i++)
            {
                var hp = health.GetBodyPartHealth(AllBodyParts[i]);
                current += hp.Current;
                max += hp.Maximum;
            }

            return max > 0f ? current / max : 1f;
        }

        private void TriggerFallback()
        {
            Vector3? point = HybridFallbackResolver.GetBestRetreatPoint(_bot, _bot.LookDirection);
            if (!point.HasValue)
                return;

            BotMovementHelper.SmoothMoveTo(_bot, point.Value, false, 1f);
            BotCoverHelper.TrySetStanceFromNearbyCover(_cache, point.Value);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);
        }

        #endregion

        #region Aim Handling

        private Vector3 GetValidatedAimPosition(IPlayer? target, float time)
        {
            if (target != null && target.HealthController?.IsAlive == true && target.Transform != null)
            {
                Vector3 pos = target.Transform.position;
                if (pos != Vector3.zero)
                    return pos;
            }

            if (_bot.Memory?.LastEnemy != null && _bot.Memory.LastEnemy.CurrPosition != Vector3.zero)
                return _bot.Memory.LastEnemy.CurrPosition;

            if (time - _lastLookAroundTime > 1.5f)
            {
                float yaw = UnityEngine.Random.Range(-75f, 75f);
                float pitch = UnityEngine.Random.Range(-10f, 10f);
                Quaternion offset = Quaternion.Euler(pitch, yaw, 0f);
                _idleLookDir = offset * _bot.Transform.forward;
                _lastLookAroundTime = time;
            }

            return _bot.Position + _idleLookDir.normalized * 10f;
        }

        #endregion

        #region Weapon Range Estimation

        private float EstimateWeaponRange(Weapon? weapon)
        {
            if (weapon == null || weapon.Template == null || string.IsNullOrEmpty(weapon.Template.Name))
                return 90f;

            string name = weapon.Template.Name;

            foreach (var kv in WeaponTypeRanges)
            {
                if (name.IndexOf(kv.Key, StringComparison.OrdinalIgnoreCase) >= 0)
                    return kv.Value;
            }

            return 90f;
        }

        #endregion
    }
}
