#nullable enable

using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.InventoryLogic;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Controls tactical device logic: flashlights, lasers, NVG, thermals. Handles toggling based on light, danger, fog, and chaos traits.
    /// </summary>
    public class BotTacticalDeviceController : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private readonly List<Item> _attachedDevices = new();
        private MethodInfo? _toggleMethod;

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

        #region Public Entry Point

        /// <summary>
        /// Called each AI tick to decide tactical device behavior.
        /// </summary>
        public void UpdateTacticalLogic(BotOwner bot, BotComponentCache cache)
        {
            if (Time.time < _nextDecisionTime || bot.IsDead)
                return;

            // 🛑 Skip human players and FIKA coop players entirely
            if (bot.GetPlayer != null && bot.GetPlayer.IsYourPlayer)
                return;

            _bot = bot;
            _cache = cache;
            _nextDecisionTime = Time.time + CheckInterval;

            if (bot.WeaponManager?.CurrentWeapon != null)
                ScanForToggles(bot.WeaponManager.CurrentWeapon);

            bool isDark = IsDarkEnvironment();
            bool shouldBait = ShouldBaitPlayer();

            foreach (var item in _attachedDevices)
            {
                string name = item.Template?.Name?.ToLower() ?? "";

                if (name.Contains("nvg"))
                    ToggleDevice(item, isDark);

                if (name.Contains("thermal"))
                    ToggleDevice(item, isDark);

                if (name.Contains("light") || name.Contains("flash") || name.Contains("laser"))
                {
                    ToggleDevice(item, isDark || shouldBait);
                }
            }

            if (shouldBait)
                Invoke(nameof(DisableAllDevices), 1.5f); // simulate fakeout
        }

        /// <summary>
        /// Turns off all devices this frame.
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

        private void ScanForToggles(Weapon weapon)
        {
            _attachedDevices.Clear();

            foreach (Slot slot in weapon.AllSlots)
            {
                if (slot.ContainedItem == null)
                    continue;

                Item mod = slot.ContainedItem;
                string lower = mod.Template?.Name?.ToLower() ?? "";

                foreach (string keyword in ToggleKeywords)
                {
                    if (lower.Contains(keyword))
                    {
                        _attachedDevices.Add(mod);
                        break;
                    }
                }
            }
        }

        private void ToggleDevice(Item item, bool on)
        {
            if (_toggleMethod == null || item == null)
                return;

            var toggleProp = item.GetType().GetProperty("On", BindingFlags.Public | BindingFlags.Instance);
            if (toggleProp == null || !toggleProp.CanRead || !toggleProp.CanWrite)
                return;

            bool current = (bool)toggleProp.GetValue(item);
            if (current != on)
                toggleProp.SetValue(item, on);
        }

        private bool IsDarkEnvironment()
        {
            float ambient = RenderSettings.ambientLight.grayscale;
            float fogDensity = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
            return ambient < LightThreshold || fogDensity > FogThreshold;
        }

        private bool ShouldBaitPlayer()
        {
            if (_cache?.AIRefactoredBotOwner?.PersonalityProfile == null)
                return false;

            float chaos = _cache.AIRefactoredBotOwner.PersonalityProfile.ChaosFactor;
            return Random.value < chaos * 0.25f;
        }

        #endregion
    }
}
