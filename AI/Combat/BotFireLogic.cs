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

        private static readonly EBodyPart[] BodyParts = (EBodyPart[])Enum.GetValues(typeof(EBodyPart));

        public BotFireLogic(BotOwner botOwner)
        {
            _botOwner = botOwner;
            _cache = botOwner.GetComponent<BotComponentCache>();
        }

        public void UpdateShootingBehavior()
        {
            if (!_botOwner.IsAI || _botOwner.Memory == null || _botOwner.WeaponManager == null)
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

        private float GetEffectiveEngageRange(Weapon weapon, BotPersonalityProfile profile)
        {
            float weaponRange = EstimateWeaponRange(weapon);
            return Mathf.Min(profile.EngagementRange, weaponRange, ENGAGE_LIMIT);
        }

        private float EstimateWeaponRange(Weapon weapon)
        {
            if (weapon.Template?.Name == null)
                return 60f;

            string name = weapon.Template.Name.ToLowerInvariant();
            foreach (var kvp in WeaponRanges)
            {
                if (name.Contains(kvp.Key))
                    return kvp.Value;
            }

            return 60f;
        }

        private bool SupportsFireMode(Weapon weapon, Weapon.EFireMode mode)
        {
            Weapon.EFireMode[] fireModes = weapon.WeapFireType;
            for (int i = 0; i < fireModes.Length; i++)
            {
                if (fireModes[i] == mode)
                    return true;
            }
            return false;
        }

        private void TrySetFireMode(BotWeaponInfo info, Weapon.EFireMode mode)
        {
            if (info.weapon.SelectedFireMode != mode)
                info.ChangeFireMode(mode);
        }

        private void ApplyScatter(GClass592 core, bool underFire, BotPersonalityProfile profile)
        {
            float composure = _cache?.PanicHandler?.GetComposureLevel() ?? 1f;
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

            float current = 0f, max = 0f;

            for (int i = 0; i < BodyParts.Length; i++)
            {
                var hp = hc.GetBodyPartHealth(BodyParts[i]);
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
    }
}
