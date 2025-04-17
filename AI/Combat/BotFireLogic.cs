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
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Controls bot fire behavior, including fire mode, accuracy, suppression response, and panic retreat logic.
    /// </summary>
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

        private static readonly Dictionary<string, float> WeaponRanges = new Dictionary<string, float>
        {
            { "sniper", 120f }, { "marksman", 120f }, { "assault", 90f }, { "rifle", 90f },
            { "smg", 50f }, { "pistol", 35f }, { "shotgun", 40f }
        };

        #endregion

        #region Constructor

        public BotFireLogic(BotOwner botOwner)
        {
            _botOwner = botOwner;
            _cache = botOwner.GetComponent<BotComponentCache>();
        }

        #endregion

        #region Main Logic

        public void UpdateShootingBehavior()
        {
            if (_botOwner == null || !_botOwner.IsAI || _botOwner.Memory == null || _botOwner.WeaponManager == null)
                return;

            var weapon = _botOwner.WeaponManager.CurrentWeapon;
            var weaponInfo = _botOwner.WeaponManager._currentWeaponInfo;
            var shootData = _botOwner.ShootData;
            var core = _botOwner.Settings?.FileSettings?.Core as GClass592;

            if (weapon == null || weaponInfo == null || shootData == null || core == null)
                return;

            var profile = BotRegistry.Get(_botOwner.ProfileId);
            if (profile == null)
                return;

            var targetPos = _botOwner.Memory.GoalEnemy?.CurrPosition ?? Vector3.zero;
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
                BotMovementHelper.SmoothMoveTo(_botOwner, targetPos, false, profile.Cohesion);
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

        #endregion

        #region Engagement Logic

        private float GetEffectiveEngageRange(Weapon weapon, BotPersonalityProfile profile)
        {
            return Mathf.Min(profile.EngagementRange, EstimateWeaponRange(weapon), ENGAGE_LIMIT);
        }

        private float EstimateWeaponRange(Weapon weapon)
        {
            string name = weapon.Template?.Name?.ToLowerInvariant() ?? "";
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

        #region Accuracy and Fire Cadence

        private void ApplyScatter(GClass592 core, bool underFire, BotPersonalityProfile profile)
        {
            float composure = 1f;
            var panicHandler = _cache?.PanicHandler;
            if (panicHandler != null)
                composure = panicHandler.GetComposureLevel();

            float suppressionFactor = underFire ? (1f - profile.AccuracyUnderFire) * (1f - composure) : 0f;
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
            float chaos = UnityEngine.Random.Range(-0.1f, 0.3f) * profile.ChaosFactor;
            return Mathf.Clamp(baseDelay + chaos, 0.25f, 1.2f);
        }

        #endregion

        #region Suppression + Retreat

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

            float current = 0f;
            float max = 0f;

            foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
            {
                var hp = hc.GetBodyPartHealth(part);
                current += hp.Current;
                max += hp.Maximum;
            }

            return max > 0f ? Mathf.Clamp01(current / max) : 1f;
        }

        private void RetreatToSafety(BotPersonalityProfile profile)
        {
            if (Time.time < _lastRetreatTime + RETREAT_COOLDOWN)
                return;

            Vector3 dir = -_botOwner.LookDirection.normalized;
            Vector3 pos = _botOwner.Position + dir * 8f;

            if (Physics.Raycast(_botOwner.Position, dir, out RaycastHit hit, 8f))
                pos = hit.point - dir;

            if (_cache?.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(_botOwner, dir, _cache.PathCache);
                if (path.Count > 0)
                    pos = path[path.Count - 1];
            }

            BotMovementHelper.SmoothMoveTo(_botOwner, pos, false, profile.Cohesion);
            _lastRetreatTime = Time.time;
        }

        #endregion
    }
}
