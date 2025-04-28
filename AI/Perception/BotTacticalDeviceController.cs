#nullable enable

namespace AIRefactored.AI.Perception
{
    using System;
    using System.Collections.Generic;

    using AIRefactored.AI.Core;

    using EFT;
    using EFT.InventoryLogic;

    using UnityEngine;

    using Random = UnityEngine.Random;

    /// <summary>
    ///     Manages flashlight, laser, NVG, and thermal toggling.
    ///     Reacts to ambient light, fog density, and chaos-driven bait behavior.
    /// </summary>
    public sealed class BotTacticalDeviceController
    {
        private readonly List<LightComponent> _devices = new();

        private BotOwner? _bot;

        private BotComponentCache? _cache;

        private float _nextDecisionTime;

        public void Initialize(BotComponentCache cache)
        {
            if (cache == null || cache.Bot == null)
                throw new InvalidOperationException(
                    "Cannot initialize BotTacticalDeviceController without valid bot cache.");

            this._cache = cache;
            this._bot = cache.Bot;
        }

        public void Tick()
        {
            if (!this.CanThink())
                return;

            this._nextDecisionTime = Time.time + TacticalConfig.CheckInterval;

            var weapon = this._bot!.WeaponManager?.CurrentWeapon;
            if (weapon == null || weapon.AllSlots == null)
                return;

            this.ScanMods(weapon);

            var isLowVisibility = this.IsLowVisibility();
            var baitTrigger = Random.value < this.ChaosBaitChance();
            var shouldEnable = isLowVisibility || baitTrigger;

            for (var i = 0; i < this._devices.Count; i++)
            {
                var device = this._devices[i];
                var state = device.GetLightState();
                if (state.IsActive != shouldEnable)
                {
                    state.IsActive = shouldEnable;
                    device.SetLightState(state);
                }
            }

            if (baitTrigger)
            {
                // Flash briefly then turn off
                this._nextDecisionTime = Time.time + 1.5f;
                for (var i = 0; i < this._devices.Count; i++)
                {
                    var device = this._devices[i];
                    var state = device.GetLightState();
                    state.IsActive = false;
                    device.SetLightState(state);
                }
            }
        }

        private bool CanThink()
        {
            return this._bot is { IsDead: false } && this._cache != null
                                                  && this._bot.GetPlayer is { IsYourPlayer: false }
                                                  && Time.time >= this._nextDecisionTime;
        }

        private float ChaosBaitChance()
        {
            return (this._cache?.AIRefactoredBotOwner?.PersonalityProfile.ChaosFactor ?? 0f) * 0.25f;
        }

        private bool IsLowVisibility()
        {
            var ambientLight = RenderSettings.ambientLight.grayscale;
            var fogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            return ambientLight < TacticalConfig.LightThreshold || fogDensity > TacticalConfig.FogThreshold;
        }

        private void ScanMods(Weapon weapon)
        {
            this._devices.Clear();

            foreach (var slot in weapon.AllSlots)
            {
                var mod = slot?.ContainedItem;
                if (mod == null)
                    continue;

                var name = mod.Template?.Name?.ToLowerInvariant() ?? string.Empty;
                var isTactical = false;
                for (var j = 0; j < TacticalConfig.Keywords.Length; j++)
                    if (name.Contains(TacticalConfig.Keywords[j]))
                    {
                        isTactical = true;
                        break;
                    }

                if (!isTactical)
                    continue;

                if (mod is FlashlightItemClass fl && fl.Light != null) this._devices.Add(fl.Light);
                else if (mod is TacticalComboItemClass combo && combo.Light != null) this._devices.Add(combo.Light);
                else if (mod is LightLaserItemClass laser && laser.Light != null) this._devices.Add(laser.Light);
            }
        }

        private static class TacticalConfig
        {
            public const float CheckInterval = 2f;

            public const float FogThreshold = 0.5f;

            public const float LightThreshold = 0.3f;

            public static readonly string[] Keywords = { "light", "laser", "nvg", "thermal", "flash" };
        }
    }
}