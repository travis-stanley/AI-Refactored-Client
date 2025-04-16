#nullable enable

using EFT;
using EFT.InventoryLogic;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;


namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Controls fire decision-making for bots: cadence, fire mode, suppression, and fallback.
    /// </summary>
    public class BotFireLogic
    {
        private readonly BotOwner _botOwner;
        private readonly BotComponentCache? _cache;

        private const float FULL_AUTO_MAX = 40f;
        private const float BURST_MAX = 100f;
        private const float ENGAGE_LIMIT = 120f;
        private const float SCATTER_MULTIPLIER = 1.15f;
        private const float SCATTER_RECOVERY = 0.95f;

        private float _nextFireDecisionTime = 0f;

        public BotFireLogic(BotOwner botOwner)
        {
            _botOwner = botOwner;
            _cache = botOwner.GetComponent<BotComponentCache>();
        }

        public void UpdateShootingBehavior()
        {
            if (_botOwner?.Memory?.GoalEnemy == null || _botOwner.WeaponManager == null)
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

            float distance = Vector3.Distance(_botOwner.Position, _botOwner.Memory.GoalEnemy.CurrPosition);
            float maxEngageRange = GetEffectiveEngageRange(weapon, profile);
            bool underSuppression = _botOwner.Memory.IsUnderFire;

            if (ShouldPanic(profile, underSuppression))
            {
                RetreatToSafety();
                return;
            }

            if (distance > maxEngageRange && !CanOverrideSuppression(profile, underSuppression))
            {
                _botOwner.GoToPoint(_botOwner.Memory.GoalEnemy.CurrPosition, false);
                return;
            }

            if (Time.time < _nextFireDecisionTime)
                return;

            float cadence = GetBurstCadence(profile);
            _nextFireDecisionTime = Time.time + cadence;

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
            string name = weapon.Template?.Name?.ToLower() ?? "";

            if (name.Contains("sniper") || name.Contains("marksman")) return 120f;
            if (name.Contains("assault") || name.Contains("rifle")) return 90f;
            if (name.Contains("smg")) return 50f;
            if (name.Contains("pistol")) return 35f;
            if (name.Contains("shotgun")) return 40f;

            return 60f;
        }

        private bool SupportsFireMode(Weapon weapon, Weapon.EFireMode mode)
        {
            foreach (var available in weapon.WeapFireType)
            {
                if (available == mode)
                    return true;
            }
            return false;
        }

        private void TrySetFireMode(BotWeaponInfo info, Weapon.EFireMode mode)
        {
            if (info.weapon.SelectedFireMode != mode)
            {
                info.ChangeFireMode(mode);
            }
        }

        private void ApplyScatter(GClass592 core, bool underFire, BotPersonalityProfile profile)
        {
            float suppressionFactor = underFire ? 1f - profile.AccuracyUnderFire : 0f;
            float scatterBoost = 1f + (SCATTER_MULTIPLIER - 1f) + suppressionFactor;
            core.ScatteringPerMeter *= scatterBoost;
        }

        private void RecoverAccuracy(GClass592 core)
        {
            core.ScatteringPerMeter *= SCATTER_RECOVERY;
        }

        private float GetBurstCadence(BotPersonalityProfile profile)
        {
            float baseDelay = Mathf.Lerp(0.8f, 0.3f, profile.AggressionLevel);
            float chaosWobble = Random.Range(-0.1f, 0.3f) * profile.ChaosFactor;
            return Mathf.Clamp(baseDelay + chaosWobble, 0.25f, 1.2f);
        }

        private bool CanOverrideSuppression(BotPersonalityProfile profile, bool isUnderFire)
        {
            if (!isUnderFire) return false;

            if (profile.IsFrenzied || profile.IsStubborn)
                return true;

            if (Random.value < profile.ChaosFactor)
                return true;

            if (profile.RiskTolerance > 0.6f && profile.AccuracyUnderFire > 0.5f)
                return true;

            return false;
        }

        private bool ShouldPanic(BotPersonalityProfile profile, bool isUnderFire)
        {
            if (!isUnderFire) return false;
            float healthRatio = GetBotHealthRatio();
            return healthRatio <= profile.RetreatThreshold;
        }

        private float GetBotHealthRatio()
        {
            var hc = _botOwner.GetPlayer?.HealthController;
            if (hc == null) return 1f;

            float totalCurrent = 0f;
            float totalMax = 0f;

            foreach (EBodyPart part in System.Enum.GetValues(typeof(EBodyPart)))
            {
                var hp = hc.GetBodyPartHealth(part);
                totalCurrent += hp.Current;
                totalMax += hp.Maximum;
            }

            return Mathf.Clamp01(totalCurrent / totalMax);
        }

        private void RetreatToSafety()
        {
            Vector3 away = _botOwner.Position - _botOwner.Memory.GoalEnemy.CurrPosition;
            Vector3 fallback = _botOwner.Position + away.normalized * 10f;

            _botOwner.FallbackTo(fallback);
        }
    }
}
