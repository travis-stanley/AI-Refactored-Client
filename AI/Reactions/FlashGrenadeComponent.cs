#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Detects intense directional light sources (e.g. flashlights, flashbangs) and simulates temporary blindness.
    /// Applies suppression and panic responses based on exposure severity and angle.
    /// </summary>
    public class FlashGrenadeComponent
    {
        #region Public Properties

        /// <summary>
        /// Gets the active bot instance linked to this flash component.
        /// </summary>
        public BotOwner? Bot { get; private set; }

        #endregion

        #region Private Fields

        private BotComponentCache? _cache;

        private float _lastFlashTime = -999f;
        private bool _isBlinded = false;

        private const float BaseBlindDuration = 4.5f;
        private const float FlashlightThresholdAngle = 25f;
        private const float FlashlightMinIntensity = 2.0f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;
        private static readonly bool DebugEnabled = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Assigns cache references to prepare the flash component.
        /// </summary>
        /// <param name="cache">Bot component cache instance.</param>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            Bot = cache.Bot;
        }

        #endregion

        #region External Tick

        /// <summary>
        /// Called each frame to track light exposure and recover from blindness over time.
        /// </summary>
        /// <param name="time">Current world time.</param>
        public void Tick(float time)
        {
            if (Bot == null || Bot.HealthController == null || Bot.GetPlayer?.IsAI != true)
                return;

            CheckFlashlightExposure();

            if (_isBlinded && time - _lastFlashTime > GetBlindRecoveryDuration())
            {
                _isBlinded = false;

                if (DebugEnabled)
                    Logger.LogDebug($"[FlashGrenadeComponent] Bot {Bot.Profile?.Id} recovered from blindness.");
            }
        }

        #endregion

        #region Flashlight Detection

        /// <summary>
        /// Checks all visible flashlights for direct eye exposure on this bot.
        /// </summary>
        private void CheckFlashlightExposure()
        {
            if (Bot?.IsDead != false || _cache == null)
                return;

            Transform? head = BotCacheUtility.Head(_cache);
            if (head == null)
                return;

            Vector3 eyePos = head.position;
            Vector3 lookDir = head.forward;

            foreach (Light? light in FlashlightRegistry.GetActiveFlashlights())
            {
                if (light == null || !light.enabled || light.intensity < FlashlightMinIntensity)
                    continue;

                Vector3 toLight = light.transform.position - eyePos;
                float angle = Vector3.Angle(lookDir, toLight.normalized);

                if (angle <= FlashlightThresholdAngle)
                {
                    if (DebugEnabled)
                    {
                        string id = Bot.Profile?.Id ?? "unknown";
                        Logger.LogDebug($"[FlashGrenadeComponent] Bot {id} blinded at angle {angle:F1}°");
                    }

                    AddBlindEffect(BaseBlindDuration, light.transform.position);
                    break;
                }
            }
        }

        #endregion

        #region Flash Reaction Logic

        /// <summary>
        /// Returns true if the bot is currently affected by flash blindness.
        /// </summary>
        public bool IsFlashed() => _isBlinded;

        /// <summary>
        /// Applies a flash effect with duration and triggers suppression.
        /// </summary>
        /// <param name="duration">Duration in seconds of the blind state.</param>
        /// <param name="source">World position of the light source.</param>
        public void AddBlindEffect(float duration, Vector3 source)
        {
            if (Bot == null || Bot.GetPlayer?.IsAI != true)
                return;

            _lastFlashTime = Time.time;
            _isBlinded = true;

            if (DebugEnabled)
                Logger.LogDebug($"[FlashGrenadeComponent] Blind applied to {Bot.Profile?.Id} for {duration:F1}s");

            BotSuppressionHelper.TrySuppressBot(Bot.GetPlayer, source);
        }

        /// <summary>
        /// Calculates how long blindness should last based on composure.
        /// </summary>
        private float GetBlindRecoveryDuration()
        {
            float composure = _cache?.PanicHandler?.GetComposureLevel() ?? 1f;
            return Mathf.Lerp(2.0f, BaseBlindDuration, 1f - composure);
        }

        #endregion
    }
}
