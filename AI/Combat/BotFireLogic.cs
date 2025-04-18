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
    /// <summary>
    /// Controls bot firing logic including fire mode selection, accuracy modulation, and panic retreat.
    /// Behavior is fully personality-driven and compatible with BotBrain tick system.
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

        private static readonly Dictionary<string, float> WeaponRanges = new()
        {
            { "sniper", 120f }, { "marksman", 120f }, { "assault", 90f }, { "rifle", 90f },
            { "smg", 50f }, { "pistol", 35f }, { "shotgun", 40f }
        };

        private static readonly EBodyPart[] BodyParts = (EBodyPart[])Enum.GetValues(typeof(EBodyPart));

        #endregion

        #region Constructor

        public BotFireLogic(BotOwner botOwner)
        {
            _botOwner = botOwner;
            _cache = botOwner.GetComponent<BotComponentCache>();
        }

        #endregion

        #region Tick

        public void Tick(float time)
        {
            if (_botOwner == null || !_botOwner.IsAI || _botOwner.IsDead)
                return;

            if (_botOwner.Memory == null || _botOwner.WeaponManager == null || _botOwner.ShootData == null)
                return;

            Weapon weapon = _botOwner.WeaponManager.CurrentWeapon;
            BotWeaponInfo weaponInfo = _botOwner.WeaponManager._currentWeaponInfo;
            GClass592 core = _botOwner.Settings?.FileSettings?.Core as GClass592;

            if (weapon == null || weaponInfo == null || core == null)
                return;

            BotPersonalityProfile profile = BotRegistry.Get(_botOwner.ProfileId);
            if (profile == null)
                return;

            Vector3 targetPos = _botOwner.Memory.GoalEnemy?.CurrPosition ?? Vector3.zero;
            float distance = Vector3.Distance(_botOwner.Position, targetPos);
            float maxEngage = GetEffectiveEngageRange(weapon, profile);
            bool underFire = _botOwner.Memory.IsUnderFire;

            if (ShouldPanic(profile, underFire))
            {
                TryRetreat(profile);
                return;
            }

            if (distance > maxEngage && !CanOverrideSuppression(profile, underFire))
            {
                BotMovementHelper.SmoothMoveTo(_botOwner, targetPos, false, profile.Cohesion);
                return;
            }

            if (time < _nextFireDecisionTime)
                return;

            _nextFireDecisionTime = time + GetBurstCadence(profile);

            // === Fire Mode Selection ===
            if (distance <= FULL_AUTO_MAX)
            {
                SetFireMode(weaponInfo, Weapon.EFireMode.fullauto);
                RecoverAccuracy(core);
            }
            else if (distance <= BURST_MAX && SupportsFireMode(weapon, Weapon.EFireMode.burst))
            {
                SetFireMode(weaponInfo, Weapon.EFireMode.burst);
                ApplyScatter(core, underFire, profile);
            }
            else
            {
                SetFireMode(weaponInfo, Weapon.EFireMode.single);
                ApplyScatter(core, underFire, profile);
            }

            _botOwner.ShootData.Shoot();
        }

        #endregion

        #region Engagement Ranges

        private float GetEffectiveEngageRange(Weapon weapon, BotPersonalityProfile profile)
        {
            return Mathf.Min(profile.EngagementRange, EstimateWeaponRange(weapon), ENGAGE_LIMIT);
        }

        private float EstimateWeaponRange(Weapon weapon)
        {
            if (weapon.Template == null || string.IsNullOrEmpty(weapon.Template.Name))
                return 60f;

            string name = weapon.Template.Name.ToLowerInvariant();
            foreach (KeyValuePair<string, float> kvp in WeaponRanges)
            {
                if (name.Contains(kvp.Key))
                    return kvp.Value;
            }

            return 60f;
        }

        private bool SupportsFireMode(Weapon weapon, Weapon.EFireMode mode)
        {
            Weapon.EFireMode[] modes = weapon.WeapFireType;
            for (int i = 0; i < modes.Length; i++)
            {
                if (modes[i] == mode)
                    return true;
            }
            return false;
        }

        private void SetFireMode(BotWeaponInfo info, Weapon.EFireMode mode)
        {
            if (info.weapon.SelectedFireMode != mode)
                info.ChangeFireMode(mode);
        }

        #endregion

        #region Accuracy + Scatter

        private void ApplyScatter(GClass592 core, bool underFire, BotPersonalityProfile profile)
        {
            float composure = _cache?.PanicHandler?.GetComposureLevel() ?? 1f;
            float penalty = underFire ? (1f - profile.AccuracyUnderFire) * (1f - composure) : 0f;
            float scatter = SCATTER_MULTIPLIER + penalty;

            core.ScatteringPerMeter = Mathf.Min(core.ScatteringPerMeter * scatter, MAX_SCATTER);
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

        #region Suppression & Retreat Logic

        private bool CanOverrideSuppression(BotPersonalityProfile profile, bool underFire)
        {
            return underFire &&
                   (profile.IsFrenzied || profile.IsStubborn ||
                    UnityEngine.Random.value < profile.ChaosFactor ||
                    (profile.RiskTolerance > 0.6f && profile.AccuracyUnderFire > 0.5f));
        }

        private bool ShouldPanic(BotPersonalityProfile profile, bool underFire)
        {
            return underFire && GetBotHealthRatio() <= profile.RetreatThreshold;
        }

        private float GetBotHealthRatio()
        {
            Player? player = _botOwner.GetPlayer;
            if (player == null || player.HealthController == null)
                return 1f;

            float current = 0f;
            float max = 0f;

            for (int i = 0; i < BodyParts.Length; i++)
            {
                var hp = player.HealthController.GetBodyPartHealth(BodyParts[i]);
                current += hp.Current;
                max += hp.Maximum;
            }

            return max > 0f ? Mathf.Clamp01(current / max) : 1f;
        }

        private void TryRetreat(BotPersonalityProfile profile)
        {
            if (Time.time < _lastRetreatTime + RETREAT_COOLDOWN)
                return;

            Vector3 dir = -_botOwner.LookDirection.normalized;
            Vector3 fallback = _botOwner.Position + dir * 8f;

            if (Physics.Raycast(_botOwner.Position, dir, out RaycastHit hit, 8f))
                fallback = hit.point - dir;

            if (_cache?.PathCache != null)
            {
                List<Vector3> path = BotCoverRetreatPlanner.GetCoverRetreatPath(_botOwner, dir, _cache.PathCache);
                if (path.Count > 0)
                    fallback = path[path.Count - 1];
            }

            BotMovementHelper.SmoothMoveTo(_botOwner, fallback, false, profile.Cohesion);
            _lastRetreatTime = Time.time;
        }

        #endregion
    }
}
