#nullable enable

using UnityEngine;
using EFT;
using System;
using System.Collections.Generic;
using EFT.InventoryLogic;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Optimization;

namespace AIRefactored.AI.Combat
{
    public class BotFireLogic
    {
        #region Fields

        private readonly BotOwner _botOwner;
        private readonly BotComponentCache? _cache;

        private const float FULL_AUTO_MAX = 40f;
        private const float BURST_MAX = 100f;
        private const float ENGAGE_LIMIT = 120f;
        private const float SCATTER_MULTIPLIER = 1.15f;
        private const float SCATTER_RECOVERY = 0.95f;
        private const float MAX_SCATTER = 4.0f;
        private const float RETREAT_COOLDOWN = 4.0f;

        private float _nextFireDecisionTime = 0f;
        private float _lastRetreatTime = -999f;

        private static readonly Dictionary<string, float> WeaponRanges = new()
        {
            { "sniper", 120f }, { "marksman", 120f }, { "assault", 90f }, { "rifle", 90f },
            { "smg", 50f }, { "pistol", 35f }, { "shotgun", 40f }
        };

        #endregion

        public BotFireLogic(BotOwner botOwner)
        {
            _botOwner = botOwner;
            _cache = botOwner.GetComponent<BotComponentCache>();
        }

        public void UpdateShootingBehavior()
        {
            if (_botOwner?.Memory?.GoalEnemy == null || _botOwner.WeaponManager?.CurrentWeapon == null || !_botOwner.IsAI)
                return;

            var weapon = _botOwner.WeaponManager.CurrentWeapon;
            var weaponInfo = _botOwner.WeaponManager._currentWeaponInfo;
            var shootData = _botOwner.ShootData;
            var core = _botOwner.Settings?.FileSettings?.Core as GClass592;

            if (weaponInfo == null || shootData == null || core == null)
                return;

            var profile = BotRegistry.Get(_botOwner.ProfileId);
            if (profile == null)
                return;

            var targetPos = _botOwner.Memory.GoalEnemy.CurrPosition;
            float distance = Vector3.Distance(_botOwner.Position, targetPos);
            float maxEngageRange = GetEffectiveEngageRange(weapon, profile);
            bool underSuppression = _botOwner.Memory.IsUnderFire;

            if (ShouldPanic(profile, underSuppression))
            {
                RetreatToSafety(profile);
                return;
            }

            if (distance > maxEngageRange && !CanOverrideSuppression(profile, underSuppression))
            {
                BotMovementHelper.SmoothMoveTo(_botOwner, targetPos, allowSlowEnd: false, cohesionScale: profile.Cohesion);
                return;
            }

            if (Time.time < _nextFireDecisionTime)
                return;

            _nextFireDecisionTime = Time.time + GetBurstCadence(profile);

            if (distance <= FULL_AUTO_MAX)
            {
                TrySetFireMode(weaponInfo, Weapon.EFireMode.fullauto);
                RecoverAccuracy(core);
            }
            else if (distance <= BURST_MAX && SupportsFireMode(weapon, Weapon.EFireMode.burst))
            {
                TrySetFireMode(weaponInfo, Weapon.EFireMode.burst);
                ApplyScatter(core, underSuppression, profile);
            }
            else
            {
                TrySetFireMode(weaponInfo, Weapon.EFireMode.single);
                ApplyScatter(core, underSuppression, profile);
            }

            shootData.Shoot();
        }

        #region Engagement Logic

        private float GetEffectiveEngageRange(Weapon weapon, BotPersonalityProfile profile)
        {
            return Mathf.Min(profile.EngagementRange, EstimateWeaponRange(weapon), ENGAGE_LIMIT);
        }

        private float EstimateWeaponRange(Weapon weapon)
        {
            string? name = weapon.Template?.Name;
            if (string.IsNullOrEmpty(name))
                return 60f;

            name = name.ToLowerInvariant();

            foreach (var pair in WeaponRanges)
            {
                if (name.Contains(pair.Key))
                    return pair.Value;
            }

            return 60f;
        }

        private bool SupportsFireMode(Weapon weapon, Weapon.EFireMode mode)
        {
            return Array.Exists(weapon.WeapFireType, f => f == mode);
        }

        private void TrySetFireMode(BotWeaponInfo info, Weapon.EFireMode mode)
        {
            if (info.weapon.SelectedFireMode != mode)
                info.ChangeFireMode(mode);
        }

        #endregion

        #region Accuracy + Cadence

        private void ApplyScatter(GClass592 core, bool underFire, BotPersonalityProfile profile)
        {
            float suppressionFactor = underFire ? 1f - profile.AccuracyUnderFire : 0f;
            float scatterBoost = 1f + (SCATTER_MULTIPLIER - 1f) + suppressionFactor;

            core.ScatteringPerMeter *= scatterBoost;
            if (core.ScatteringPerMeter > MAX_SCATTER)
                core.ScatteringPerMeter = MAX_SCATTER;
        }

        private void RecoverAccuracy(GClass592 core)
        {
            core.ScatteringPerMeter *= SCATTER_RECOVERY;
        }

        private float GetBurstCadence(BotPersonalityProfile profile)
        {
            float baseDelay = Mathf.Lerp(0.8f, 0.3f, profile.AggressionLevel);
            float chaosWobble = UnityEngine.Random.Range(-0.1f, 0.3f) * profile.ChaosFactor;
            return Mathf.Clamp(baseDelay + chaosWobble, 0.25f, 1.2f);
        }

        #endregion

        #region Suppression/Panic Overrides

        private bool CanOverrideSuppression(BotPersonalityProfile profile, bool isUnderFire)
        {
            return isUnderFire && (
                profile.IsFrenzied || profile.IsStubborn ||
                UnityEngine.Random.value < profile.ChaosFactor ||
                (profile.RiskTolerance > 0.6f && profile.AccuracyUnderFire > 0.5f)
            );
        }

        private bool ShouldPanic(BotPersonalityProfile profile, bool isUnderFire)
        {
            return isUnderFire && GetBotHealthRatio() <= profile.RetreatThreshold;
        }

        private float GetBotHealthRatio()
        {
            var hc = _botOwner.GetPlayer?.HealthController;
            if (hc == null) return 1f;

            float totalCurrent = 0f;
            float totalMax = 0f;

            foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
            {
                var hp = hc.GetBodyPartHealth(part);
                totalCurrent += hp.Current;
                totalMax += hp.Maximum;
            }

            return Mathf.Clamp01(totalCurrent / totalMax);
        }

        private void RetreatToSafety(BotPersonalityProfile profile)
        {
            if (Time.time < _lastRetreatTime + RETREAT_COOLDOWN)
                return;

            Vector3 fallbackDir = -_botOwner.LookDirection.normalized;
            Vector3 fallbackPos = _botOwner.Position + fallbackDir * 8f;

            if (Physics.Raycast(_botOwner.Position, fallbackDir, out var hit, 8f))
                fallbackPos = hit.point - fallbackDir;

            if (_cache?.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(_botOwner, fallbackDir, _cache.PathCache);
                if (path.Count > 0)
                    fallbackPos = path[path.Count - 1];
            }

            BotMovementHelper.SmoothMoveTo(_botOwner, fallbackPos, false, profile.Cohesion);
            _lastRetreatTime = Time.time;
        }

        #endregion
    }
}
