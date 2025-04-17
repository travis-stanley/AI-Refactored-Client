#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using EFT;
using EFT.InventoryLogic;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Controls bot tactical device behavior: flashlights, lasers, NVG, thermals.
    /// Decides toggling based on lighting, fog, chaos factor, and baiting behavior.
    /// </summary>
    public class BotTacticalDeviceController : MonoBehaviour
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

        private static readonly string[] ToggleKeywords = { "light", "laser", "nvg", "thermal" };

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _toggleMethod = typeof(Item).GetMethod("Toggle", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        #endregion

        #region Public Entry

        /// <summary>
        /// Called each AI tick to evaluate whether tactical devices should be toggled.
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

            foreach (var item in _attachedDevices)
            {
                string name = item.Template?.Name?.ToLower() ?? "";

                if (name.Contains("nvg") || name.Contains("thermal"))
                {
                    ToggleDevice(item, isDark);
                }

                if (name.Contains("light") || name.Contains("flash") || name.Contains("laser"))
                {
                    ToggleDevice(item, isDark || baitMode);
                }
            }

            if (baitMode)
                Invoke(nameof(DisableAllDevices), 1.5f); // Fake-out tactic
        }

        /// <summary>
        /// Turns off all tactical devices on the weapon.
        /// </summary>
        public void DisableAllDevices()
        {
            foreach (var item in _attachedDevices)
            {
                ToggleDevice(item, false);
            }
        }

        #endregion

        #region Internal Logic

        /// <summary>
        /// Finds all tactical mods that can be toggled on the weapon.
        /// </summary>
        private void ScanForToggleableDevices(Weapon weapon)
        {
            _attachedDevices.Clear();

            foreach (Slot slot in weapon.AllSlots)
            {
                var mod = slot.ContainedItem;
                if (mod == null)
                    continue;

                string lowerName = mod.Template?.Name?.ToLower() ?? "";
                foreach (string keyword in ToggleKeywords)
                {
                    if (lowerName.Contains(keyword))
                    {
                        _attachedDevices.Add(mod);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Uses reflection to toggle an item on/off based on current state.
        /// </summary>
        private void ToggleDevice(Item item, bool on)
        {
            if (_toggleMethod == null || item == null)
                return;

            var toggleProp = item.GetType().GetProperty("On", BindingFlags.Public | BindingFlags.Instance);
            if (toggleProp == null || !toggleProp.CanRead || !toggleProp.CanWrite)
                return;

            bool current = (bool)(toggleProp.GetValue(item) ?? false);
            if (current != on)
            {
                toggleProp.SetValue(item, on);
            }
        }

        /// <summary>
        /// Checks environment lighting and fog to determine if it’s dark.
        /// </summary>
        private bool IsEnvironmentDark()
        {
            float ambient = RenderSettings.ambientLight.grayscale;
            float fog = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            return ambient < LightThreshold || fog > FogThreshold;
        }

        /// <summary>
        /// Determines if the bot should bait the player by briefly flashing devices.
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
