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
    /// Controls bot flashlight, laser, NVG, and thermal toggling based on light conditions and chaos behavior.
    /// Devices are detected dynamically via weapon mod slots.
    /// </summary>
    public class BotTacticalDeviceController
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private readonly List<Item> _toggleableDevices = new();
        private MethodInfo? _toggleMethod;

        private float _nextDecisionTime;

        private const float CheckInterval = 2.0f;
        private const float FogThreshold = 0.5f;
        private const float LightThreshold = 0.3f;

        private static readonly string[] ToggleKeywords = { "light", "laser", "nvg", "thermal", "flash" };
        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;
        private static readonly bool DebugEnabled = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the tactical controller with references to bot and cache.
        /// </summary>
        public void Initialize(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _toggleMethod = typeof(Item).GetMethod("Toggle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        #endregion

        #region Tick

        /// <summary>
        /// Evaluates environment and toggles tactical devices appropriately.
        /// </summary>
        public void UpdateTacticalLogic(BotOwner bot, BotComponentCache cache)
        {
            if (Time.time < _nextDecisionTime || bot.IsDead || bot.GetPlayer?.IsYourPlayer == true)
                return;

            _bot = bot;
            _cache = cache;
            _nextDecisionTime = Time.time + CheckInterval;

            Weapon? weapon = bot.WeaponManager?.CurrentWeapon;
            if (weapon == null || weapon.AllSlots == null)
                return;

            ScanForToggleableDevices(weapon);

            bool isDark = IsEnvironmentDark();
            bool baitMode = ShouldBait();

            if (DebugEnabled)
                Logger.LogDebug($"[TacticalDevice] {bot.Profile?.Info?.Nickname ?? "Bot"}: dark={isDark}, bait={baitMode}");

            for (int i = 0; i < _toggleableDevices.Count; i++)
            {
                var device = _toggleableDevices[i];
                string name = device.Template?.Name?.ToLowerInvariant() ?? "";

                bool enable = (name.Contains("nvg") || name.Contains("thermal")) ? isDark : (isDark || baitMode);
                ToggleDevice(device, enable);
            }

            // Flash bait fake-out
            if (baitMode)
            {
                _nextDecisionTime = Time.time + 1.5f;
                for (int i = 0; i < _toggleableDevices.Count; i++)
                {
                    ToggleDevice(_toggleableDevices[i], false);
                }

                if (DebugEnabled)
                    Logger.LogDebug($"[TacticalDevice] {bot.Profile?.Info?.Nickname ?? "Bot"} executed bait-light fake-out.");
            }
        }

        /// <summary>
        /// Disables all tactical devices.
        /// </summary>
        public void DisableAllDevices()
        {
            for (int i = 0; i < _toggleableDevices.Count; i++)
            {
                ToggleDevice(_toggleableDevices[i], false);
            }

            if (DebugEnabled)
                Logger.LogDebug("[TacticalDevice] Disabled all tactical devices.");
        }

        #endregion

        #region Helpers

        private void ScanForToggleableDevices(Weapon weapon)
        {
            _toggleableDevices.Clear();

            foreach (Slot? slot in weapon.AllSlots)
            {
                if (slot?.ContainedItem is not Item item)
                    continue;

                string name = item.Template?.Name?.ToLowerInvariant() ?? "";
                for (int i = 0; i < ToggleKeywords.Length; i++)
                {
                    if (name.Contains(ToggleKeywords[i]))
                    {
                        _toggleableDevices.Add(item);

                        if (DebugEnabled)
                            Logger.LogDebug($"[TacticalDevice] Found toggleable device: {item.Template?.Name}");

                        break;
                    }
                }
            }
        }

        private void ToggleDevice(Item item, bool enable)
        {
            if (_toggleMethod == null)
                return;

            var prop = item.GetType().GetProperty("On", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (prop == null || !prop.CanRead || !prop.CanWrite)
                return;

            bool isCurrentlyOn = (bool)(prop.GetValue(item) ?? false);
            if (isCurrentlyOn == enable)
                return;

            prop.SetValue(item, enable);

            if (DebugEnabled)
                Logger.LogDebug($"[TacticalDevice] {(enable ? "Enabled" : "Disabled")} device: {item.Template?.Name}");
        }

        private bool IsEnvironmentDark()
        {
            float ambient = RenderSettings.ambientLight.grayscale;
            float fogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            return ambient < LightThreshold || fogDensity > FogThreshold;
        }

        private bool ShouldBait()
        {
            float chaos = _cache?.AIRefactoredBotOwner?.PersonalityProfile?.ChaosFactor ?? 0f;
            return UnityEngine.Random.value < chaos * 0.25f;
        }

        #endregion
    }
}
