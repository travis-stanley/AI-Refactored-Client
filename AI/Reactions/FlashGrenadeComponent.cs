#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Detects sudden bright light exposure from flashlights or flashbangs.
    /// Simulates temporary blindness and optionally applies suppression/fallback.
    /// </summary>
    public sealed class FlashGrenadeComponent
    {
        #region Constants

        private const float BaseBlindDuration = 4.5f;
        private const float FlashlightMinIntensity = 2f;
        private const float MaxFlashlightAngle = 25f;

        #endregion

        #region State

        private BotComponentCache? _cache;
        private BotOwner? _bot;

        private float _lastFlashTime = -999f;
        private bool _isBlinded;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the flash component with the bot's runtime cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Runtime Tick

        /// <summary>
        /// Evaluates exposure to light and clears blindness after recovery.
        /// </summary>
        public void Tick(float time)
        {
            if (_bot?.GetPlayer?.IsAI != true || _bot.IsDead)
                return;

            CheckForFlashlightExposure();

            if (_isBlinded && time - _lastFlashTime > GetBlindRecoveryTime())
                _isBlinded = false;
        }

        /// <summary>
        /// Returns true if the bot is still considered blinded.
        /// </summary>
        public bool IsFlashed() => _isBlinded;

        #endregion

        #region External Flash Triggers

        /// <summary>
        /// Forces the bot into a blind state, optionally with suppression if a source is known.
        /// </summary>
        /// <summary>
        /// Forces the bot into a blind state, optionally with suppression if a source is known.
        /// </summary>
        public void ForceBlind(float duration = BaseBlindDuration, Vector3? source = null)
        {
            if (_bot?.IsDead != false)
                return;

            _lastFlashTime = Time.time;
            _isBlinded = true;

            if (source.HasValue)
                BotSuppressionHelper.TrySuppressBot(_bot.GetPlayer, source.Value);
        }

        #endregion

        #region Flashlight Detection

        /// <summary>
        /// Scans for nearby high-intensity flashlights within the bot's frontal cone.
        /// </summary>
        private void CheckForFlashlightExposure()
        {
            if (_cache == null || _bot == null)
                return;

            Transform? head = BotCacheUtility.Head(_cache);
            if (head == null)
                return;

            Vector3 eyePos = head.position;
            Vector3 viewDir = head.forward;

            var flashlightEnumerable = FlashlightRegistry.GetActiveFlashlights();
            if (flashlightEnumerable == null)
                return;

            var flashlights = new List<Light>(flashlightEnumerable);
            if (flashlights.Count == 0)
                return;

            for (int i = 0; i < flashlights.Count; i++)
            {
                var light = flashlights[i];
                if (light == null || !light.enabled || light.intensity < FlashlightMinIntensity)
                    continue;

                Vector3 toLight = (light.transform.position - eyePos).normalized;
                float angleToLight = Vector3.Angle(viewDir, toLight);

                if (angleToLight > MaxFlashlightAngle)
                    continue;

                _lastFlashTime = Time.time;
                _isBlinded = true;

                BotSuppressionHelper.TrySuppressBot(_bot.GetPlayer, light.transform.position);
                break;
            }
        }


        #endregion

        #region Blindness Recovery

        /// <summary>
        /// Computes recovery time based on bot composure.
        /// Higher composure leads to faster recovery.
        /// </summary>
        private float GetBlindRecoveryTime()
        {
            float composure = _cache?.PanicHandler?.GetComposureLevel() ?? 1f;
            return Mathf.Lerp(2f, BaseBlindDuration, 1f - composure);
        }

        #endregion
    }
}
