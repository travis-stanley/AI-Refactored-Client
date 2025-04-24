#nullable enable

using AIRefactored.AI.Core;
using EFT;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Manages flashlight, laser, NVG, and thermal toggling.
    /// Reacts to ambient light, fog density, and chaos-driven bait behavior.
    /// </summary>
    public sealed class BotTacticalDeviceController
    {
        #region Config

        private static class TacticalConfig
        {
            public const float CheckInterval = 2f;
            public const float FogThreshold = 0.5f;
            public const float LightThreshold = 0.3f;

            public static readonly string[] Keywords = { "light", "laser", "nvg", "thermal", "flash" };
        }

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private readonly List<LightComponent> _devices = new();
        private float _nextDecisionTime;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            if (cache == null || cache.Bot == null)
                throw new InvalidOperationException("Cannot initialize BotTacticalDeviceController without valid bot cache.");

            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Tick

        public void Tick()
        {
            if (!CanThink())
                return;

            _nextDecisionTime = Time.time + TacticalConfig.CheckInterval;

            Weapon? weapon = _bot!.WeaponManager?.CurrentWeapon;
            if (weapon == null || weapon.AllSlots == null)
                return;

            ScanMods(weapon);

            bool isLowVisibility = IsLowVisibility();
            bool baitTrigger = UnityEngine.Random.value < ChaosBaitChance();
            bool shouldEnable = isLowVisibility || baitTrigger;

            for (int i = 0; i < _devices.Count; i++)
            {
                var device = _devices[i];
                var state = device.GetLightState(toggleActive: false, switchMod: false);
                if (state.IsActive != shouldEnable)
                {
                    state.IsActive = shouldEnable;
                    device.SetLightState(state);
                }
            }

            if (baitTrigger)
            {
                // Flash briefly then turn off
                _nextDecisionTime = Time.time + 1.5f;
                for (int i = 0; i < _devices.Count; i++)
                {
                    var device = _devices[i];
                    var state = device.GetLightState(toggleActive: false, switchMod: false);
                    state.IsActive = false;
                    device.SetLightState(state);
                }
            }
        }

        #endregion

        #region Logic

        private bool CanThink()
        {
            return _bot is { IsDead: false }
                && _cache != null
                && _bot.GetPlayer is { IsYourPlayer: false }
                && Time.time >= _nextDecisionTime;
        }

        private bool IsLowVisibility()
        {
            float ambientLight = RenderSettings.ambientLight.grayscale;
            float fogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            return ambientLight < TacticalConfig.LightThreshold || fogDensity > TacticalConfig.FogThreshold;
        }

        private float ChaosBaitChance()
        {
            return (_cache?.AIRefactoredBotOwner?.PersonalityProfile.ChaosFactor ?? 0f) * 0.25f;
        }

        private void ScanMods(Weapon weapon)
        {
            _devices.Clear();

            foreach (var slot in weapon.AllSlots)
            {
                var mod = slot?.ContainedItem;
                if (mod == null)
                    continue;

                string name = mod.Template?.Name?.ToLowerInvariant() ?? "";
                bool isTactical = false;
                for (int j = 0; j < TacticalConfig.Keywords.Length; j++)
                {
                    if (name.Contains(TacticalConfig.Keywords[j]))
                    {
                        isTactical = true;
                        break;
                    }
                }

                if (!isTactical)
                    continue;

                if (mod is FlashlightItemClass fl && fl.Light != null)
                    _devices.Add(fl.Light);
                else if (mod is TacticalComboItemClass combo && combo.Light != null)
                    _devices.Add(combo.Light);
                else if (mod is LightLaserItemClass laser && laser.Light != null)
                    _devices.Add(laser.Light);
            }
        }

        #endregion
    }
}
