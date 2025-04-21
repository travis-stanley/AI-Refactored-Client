#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Handles bot fire logic including fire mode decisions, cadence, retreat logic, and accuracy tuning.
    /// Adapts behavior based on personality, distance, suppression, and weapon type.
    /// </summary>
    public class BotFireLogic
    {
        #region Fields

        private readonly BotOwner _botOwner;
        private readonly BotComponentCache? _cache;
        private float _nextFireDecisionTime;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static readonly Dictionary<string, float> WeaponRanges = new()
        {
            { "sniper", 180f }, { "marksman", 150f }, { "assault", 100f }, { "rifle", 120f },
            { "smg", 75f }, { "pistol", 35f }, { "shotgun", 50f }
        };

        private static readonly EBodyPart[] BodyParts = (EBodyPart[])Enum.GetValues(typeof(EBodyPart));

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs fire logic for the specified bot.
        /// </summary>
        /// <param name="botOwner">Bot instance.</param>
        public BotFireLogic(BotOwner botOwner)
        {
            _botOwner = botOwner;
            _cache = botOwner.GetComponent<BotComponentCache>();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Ticks bot fire logic. Determines fire decision, cadence, accuracy, and retreat behavior.
        /// </summary>
        /// <param name="time">Current world time.</param>
        public void Tick(float time)
        {
            if (_botOwner == null || !_botOwner.IsAI || _botOwner.IsDead || _botOwner.Memory == null)
                return;

            var weaponMgr = _botOwner.WeaponManager;
            if (weaponMgr == null)
                return;

            var shootData = _botOwner.ShootData;
            if (shootData == null)
                return;

            var weaponInfo = weaponMgr._currentWeaponInfo;
            var weapon = weaponInfo?.weapon;
            var core = _botOwner.Settings?.FileSettings?.Core as GClass592;

            if (weapon == null || weaponInfo == null || core == null)
                return;

            var profile = BotRegistry.Get(_botOwner.ProfileId);
            if (profile == null)
                return;

            IPlayer? enemy = _cache?.ThreatSelector?.CurrentTarget ?? _botOwner.Memory.GoalEnemy?.Person;
            if (enemy == null || !enemy.HealthController.IsAlive)
                return;

            Vector3 targetPos = enemy.Transform.position;
            float distance = Vector3.Distance(_botOwner.Position, targetPos);
            float engageRange = Mathf.Min(profile.EngagementRange, EstimateWeaponRange(weapon), 200f);

            if (_botOwner.Memory.IsUnderFire && GetHealthRatio() <= profile.RetreatThreshold)
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
                weaponMgr.Selector.TryChangeWeaponCauseNoAmmo();
                weaponMgr.Melee.Activate();
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
                ApplyScatter(core, true, profile);
            }
            else
            {
                SetFireMode(weaponInfo, Weapon.EFireMode.single);
                ApplyScatter(core, true, profile);
            }

            if (weaponMgr.IsWeaponReady)
            {
                shootData.Shoot();
                _cache?.LastShotTracker?.RegisterShot(enemy);
            }
        }

        #endregion

        #region Internal Logic

        private void SetFireMode(BotWeaponInfo info, Weapon.EFireMode mode)
        {
            if (info.weapon.SelectedFireMode != mode)
                info.ChangeFireMode(mode);
        }

        private bool SupportsFireMode(Weapon weapon, Weapon.EFireMode mode)
        {
            foreach (var fireType in weapon.WeapFireType)
            {
                if (fireType == mode)
                    return true;
            }
            return false;
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
            float baseDelay = Mathf.Lerp(0.75f, 0.25f, profile.AggressionLevel);
            float reaction = Mathf.Lerp(0.1f, 0.35f, 1f - profile.ReactionTime);
            float chaos = UnityEngine.Random.Range(-0.1f, 0.25f) * profile.ChaosFactor;
            return Mathf.Clamp(baseDelay + chaos + reaction, 0.2f, 1.25f);
        }

        private float GetHealthRatio()
        {
            var health = _botOwner.GetPlayer?.HealthController;
            if (health == null)
                return 1f;

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
            if (_cache?.PathCache == null)
                return;

            Vector3 dir = _botOwner.LookDirection.normalized;
            List<Vector3> path = BotCoverRetreatPlanner.GetCoverRetreatPath(_botOwner, dir, _cache.PathCache);

            if (path.Count > 0)
            {
                Vector3 fallback = path[path.Count - 1];
                if (NavMesh.SamplePosition(fallback, out NavMeshHit hit, 2f, NavMesh.AllAreas))
                {
                    BotMovementHelper.SmoothMoveTo(_botOwner, hit.position, false, profile.Cohesion);
                }
            }
        }

        #endregion
    }
}
