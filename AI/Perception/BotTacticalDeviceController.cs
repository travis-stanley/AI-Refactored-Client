#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using EFT.InventoryLogic;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Controls bot tactical device usage (flashlights, lasers, NVGs, thermals).
    /// Toggles devices dynamically based on darkness, fog, and bait behavior influenced by bot personality.
    /// </summary>
    public class BotTacticalDeviceController
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private MethodInfo? _toggleMethod;

        private readonly List<Item> _attachedDevices = new();
        private float _nextDecisionTime = 0f;

        private const float CheckInterval = 2.0f;
        private const float FogThreshold = 0.5f;
        private const float LightThreshold = 0.3f;

        private static readonly string[] ToggleKeywords = { "light", "laser", "nvg", "thermal", "flash" };

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;
        private static readonly bool _debug = false;

        #endregion

        #region Initialization

        public void Initialize(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _toggleMethod = typeof(Item).GetMethod("Toggle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        #endregion

        #region Public Logic

        /// <summary>
        /// Called each tick to evaluate and update tactical device usage.
        /// </summary>
        public void UpdateTacticalLogic(BotOwner bot, BotComponentCache cache)
        {
            if (Time.time < _nextDecisionTime || bot.IsDead)
                return;

            if (bot.GetPlayer?.IsYourPlayer == true)
                return;

            _bot = bot;
            _cache = cache;
            _nextDecisionTime = Time.time + CheckInterval;

            if (bot.WeaponManager?.CurrentWeapon != null)
                ScanForToggleableDevices(bot.WeaponManager.CurrentWeapon);

            bool isDark = IsEnvironmentDark();
            bool baitMode = ShouldBaitPlayer();

            if (_debug)
                _log.LogDebug($"[TacticalDevice] {bot.Profile?.Info?.Nickname}: dark={isDark}, bait={baitMode}");

            for (int i = 0; i < _attachedDevices.Count; i++)
            {
                var item = _attachedDevices[i];
                string name = item.Template?.Name?.ToLower() ?? "";

                if (name.Contains("nvg") || name.Contains("thermal"))
                {
                    ToggleDevice(item, isDark);
                }
                else if (name.Contains("light") || name.Contains("flash") || name.Contains("laser"))
                {
                    ToggleDevice(item, isDark || baitMode);
                }
            }

            if (baitMode)
            {
                _nextDecisionTime = Time.time + 1.5f;

                for (int i = 0; i < _attachedDevices.Count; i++)
                {
                    ToggleDevice(_attachedDevices[i], false);
                }

                if (_debug)
                    _log.LogDebug($"[TacticalDevice] {bot.Profile?.Info?.Nickname} executed bait-light fake-out.");
            }
        }

        /// <summary>
        /// Immediately disables all tactical devices currently active.
        /// </summary>
        public void DisableAllDevices()
        {
            for (int i = 0; i < _attachedDevices.Count; i++)
            {
                ToggleDevice(_attachedDevices[i], false);
            }

            if (_debug)
                _log.LogDebug("[TacticalDevice] Disabled all attached tactical devices.");
        }

        #endregion

        #region Internal Logic

        /// <summary>
        /// Scans the weapon for toggleable attachments like flashlights, lasers, NVGs.
        /// </summary>
        private void ScanForToggleableDevices(Weapon weapon)
        {
            _attachedDevices.Clear();

            foreach (Slot slot in weapon.AllSlots)
            {
                var mod = slot.ContainedItem;
                if (mod == null)
                    continue;

                string name = mod.Template?.Name?.ToLower() ?? "";
                for (int i = 0; i < ToggleKeywords.Length; i++)
                {
                    if (name.Contains(ToggleKeywords[i]))
                    {
                        _attachedDevices.Add(mod);
                        if (_debug)
                            _log.LogDebug($"[TacticalDevice] Found device: {mod.Template?.Name}");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Toggles the item on or off if needed using reflection.
        /// </summary>
        private void ToggleDevice(Item item, bool enable)
        {
            if (_toggleMethod == null || item == null)
                return;

            var prop = item.GetType().GetProperty("On", BindingFlags.Instance | BindingFlags.Public);
            if (prop == null || !prop.CanRead || !prop.CanWrite)
                return;

            bool isCurrentlyOn = (bool)(prop.GetValue(item) ?? false);
            if (isCurrentlyOn != enable)
            {
                prop.SetValue(item, enable);
                if (_debug)
                    _log.LogDebug($"[TacticalDevice] {(enable ? "Enabled" : "Disabled")} device: {item.Template?.Name}");
            }
        }

        /// <summary>
        /// Checks if ambient light or fog conditions are low enough to warrant vision aid devices.
        /// </summary>
        private bool IsEnvironmentDark()
        {
            float ambient = RenderSettings.ambientLight.grayscale;
            float fog = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            return ambient < LightThreshold || fog > FogThreshold;
        }

        /// <summary>
        /// Determines if bot should intentionally bait the player using lights.
        /// Influenced by ChaosFactor.
        /// </summary>
        private bool ShouldBaitPlayer()
        {
            if (_cache?.AIRefactoredBotOwner?.PersonalityProfile == null)
                return false;

            float chaos = _cache.AIRefactoredBotOwner.PersonalityProfile.ChaosFactor;
            return UnityEngine.Random.value < chaos * 0.25f;
        }

        #endregion
    }
}
