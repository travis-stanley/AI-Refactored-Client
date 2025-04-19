#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    public class BotFireLogic
    {
        private readonly BotOwner _botOwner;
        private readonly BotComponentCache? _cache;

        private float _nextFireDecisionTime = 0f;
        private float _lastSuppressionTime = -999f;

        private static readonly Dictionary<string, float> WeaponRanges = new()
        {
            { "sniper", 180f }, { "marksman", 150f }, { "assault", 100f }, { "rifle", 120f },
            { "smg", 75f }, { "pistol", 35f }, { "shotgun", 50f }
        };

        private static readonly EBodyPart[] BodyParts = (EBodyPart[])Enum.GetValues(typeof(EBodyPart));

        public BotFireLogic(BotOwner botOwner)
        {
            _botOwner = botOwner;
            _cache = botOwner.GetComponent<BotComponentCache>();
        }

        public void Tick(float time)
        {
            if (_botOwner == null || !_botOwner.IsAI || _botOwner.IsDead || _botOwner.Memory == null)
                return;

            var weaponMgr = _botOwner.WeaponManager;
            var shootData = _botOwner.ShootData;
            var weaponInfo = weaponMgr._currentWeaponInfo;
            var weapon = weaponInfo?.weapon;
            var core = _botOwner.Settings?.FileSettings?.Core as GClass592;

            if (weapon == null || weaponInfo == null || core == null)
                return;

            var profile = BotRegistry.Get(_botOwner.ProfileId);
            if (profile == null || _botOwner.Memory.GoalEnemy == null)
                return;

            Vector3 targetPos = _botOwner.Memory.GoalEnemy.CurrPosition;
            float distance = Vector3.Distance(_botOwner.Position, targetPos);
            float engageRange = GetEffectiveEngageRange(weapon, profile);

            bool underFire = _botOwner.Memory.IsUnderFire;

            if (underFire && GetHealthRatio() <= profile.RetreatThreshold)
            {
                TryRetreat(profile);
                return;
            }

            if (distance > engageRange)
            {
                if (UnityEngine.Random.value < profile.ChaosFactor)
                {
                    BotMovementHelper.SmoothMoveTo(_botOwner, targetPos, false, profile.Cohesion);
                }
                return;
            }

            if (time < _nextFireDecisionTime)
                return;

            _nextFireDecisionTime = time + GetBurstCadence(profile);

            if (weaponInfo.BulletCount == 0 && !weaponInfo.CheckHaveAmmoForReload())
            {
                _botOwner.WeaponManager.Selector.TryChangeWeaponCauseNoAmmo();
                _botOwner.WeaponManager.Melee.Activate();
                return;
            }

            if (distance <= 40f)
            {
                SetFireMode(weaponInfo, Weapon.EFireMode.fullauto);
                RecoverAccuracy(core);
            }
            else if (distance <= 100f && SupportsFireMode(weapon, Weapon.EFireMode.burst))
            {
                SetFireMode(weaponInfo, Weapon.EFireMode.burst);
                ApplyScatter(core, underFire, profile);
            }
            else
            {
                SetFireMode(weaponInfo, Weapon.EFireMode.single);
                ApplyScatter(core, underFire, profile);
            }

            shootData.Shoot();
        }

        private float GetEffectiveEngageRange(Weapon weapon, BotPersonalityProfile profile)
        {
            return Mathf.Min(profile.EngagementRange, EstimateWeaponRange(weapon), 200f);
        }

        private float EstimateWeaponRange(Weapon weapon)
        {
            string name = weapon.Template?.Name.ToLowerInvariant() ?? string.Empty;
            foreach (var kvp in WeaponRanges)
            {
                if (name.Contains(kvp.Key))
                    return kvp.Value;
            }
            return 90f;
        }

        private bool SupportsFireMode(Weapon weapon, Weapon.EFireMode mode)
        {
            foreach (var available in weapon.WeapFireType)
                if (available == mode)
                    return true;
            return false;
        }

        private void SetFireMode(BotWeaponInfo info, Weapon.EFireMode mode)
        {
            if (info.weapon.SelectedFireMode != mode)
                info.ChangeFireMode(mode);
        }

        private void ApplyScatter(GClass592 core, bool underFire, BotPersonalityProfile profile)
        {
            float composure = _cache?.PanicHandler?.GetComposureLevel() ?? 1f;
            float scatterPenalty = underFire ? (1f - profile.AccuracyUnderFire) * (1f - composure) : 0f;
            float scatter = 1.15f + scatterPenalty;

            core.ScatteringPerMeter = Mathf.Min(core.ScatteringPerMeter * scatter, 4.0f);
        }

        private void RecoverAccuracy(GClass592 core)
        {
            core.ScatteringPerMeter *= 0.95f;
        }

        private float GetBurstCadence(BotPersonalityProfile profile)
        {
            float baseDelay = Mathf.Lerp(0.8f, 0.3f, profile.AggressionLevel);
            float chaos = UnityEngine.Random.Range(-0.1f, 0.3f) * profile.ChaosFactor;
            return Mathf.Clamp(baseDelay + chaos, 0.25f, 1.0f);
        }

        private float GetHealthRatio()
        {
            var health = _botOwner.GetPlayer?.HealthController;
            if (health == null) return 1f;

            float cur = 0f, max = 0f;
            foreach (var part in BodyParts)
            {
                var hp = health.GetBodyPartHealth(part);
                cur += hp.Current;
                max += hp.Maximum;
            }
            return max > 0f ? cur / max : 1f;
        }

        private void TryRetreat(BotPersonalityProfile profile)
        {
            Vector3 dir = -_botOwner.LookDirection.normalized;
            Vector3 fallback = _botOwner.Position + dir * 8f;

            if (Physics.Raycast(_botOwner.Position, dir, out RaycastHit hit, 8f))
                fallback = hit.point - dir;

            if (_cache?.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(_botOwner, dir, _cache.PathCache);
                if (path.Count > 0)
                    fallback = path[path.Count - 1];
            }

            BotMovementHelper.SmoothMoveTo(_botOwner, fallback, false, profile.Cohesion);
        }
    }
}
