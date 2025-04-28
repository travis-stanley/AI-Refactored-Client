#nullable enable

namespace AIRefactored.AI.Reactions
{
    using System.Collections.Generic;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Detects sudden bright light exposure from flashlights or flashbangs.
    ///     Simulates temporary blindness and optionally applies suppression/fallback.
    /// </summary>
    public sealed class FlashGrenadeComponent
    {
        private const float BaseBlindDuration = 4.5f;

        private const float FlashlightMinIntensity = 2f;

        private const float MaxFlashlightAngle = 25f;

        private BotOwner? _bot;

        private BotComponentCache? _cache;

        private bool _isBlinded;

        private float _lastFlashTime = -999f;

        /// <summary>
        ///     Forces the bot into a blind state, optionally with suppression if a source is known.
        /// </summary>
        /// <summary>
        ///     Forces the bot into a blind state, optionally with suppression if a source is known.
        /// </summary>
        public void ForceBlind(float duration = BaseBlindDuration, Vector3? source = null)
        {
            if (this._bot?.IsDead != false)
                return;

            this._lastFlashTime = Time.time;
            this._isBlinded = true;

            if (source.HasValue)
                BotSuppressionHelper.TrySuppressBot(this._bot.GetPlayer, source.Value);
        }

        /// <summary>
        ///     Initializes the flash component with the bot's runtime cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache;
            this._bot = cache.Bot;
        }

        /// <summary>
        ///     Returns true if the bot is still considered blinded.
        /// </summary>
        public bool IsFlashed()
        {
            return this._isBlinded;
        }

        /// <summary>
        ///     Evaluates exposure to light and clears blindness after recovery.
        /// </summary>
        public void Tick(float time)
        {
            if (this._bot?.GetPlayer?.IsAI != true || this._bot.IsDead)
                return;

            this.CheckForFlashlightExposure();

            if (this._isBlinded && time - this._lastFlashTime > this.GetBlindRecoveryTime()) this._isBlinded = false;
        }

        /// <summary>
        ///     Scans for nearby high-intensity flashlights within the bot's frontal cone.
        /// </summary>
        private void CheckForFlashlightExposure()
        {
            if (this._cache == null || this._bot == null)
                return;

            var head = BotCacheUtility.Head(this._cache);
            if (head == null)
                return;

            var eyePos = head.position;
            var viewDir = head.forward;

            var flashlightEnumerable = FlashlightRegistry.GetActiveFlashlights();
            if (flashlightEnumerable == null)
                return;

            var flashlights = new List<Light>(flashlightEnumerable);
            if (flashlights.Count == 0)
                return;

            for (var i = 0; i < flashlights.Count; i++)
            {
                var light = flashlights[i];
                if (light == null || !light.enabled || light.intensity < FlashlightMinIntensity)
                    continue;

                var toLight = (light.transform.position - eyePos).normalized;
                var angleToLight = Vector3.Angle(viewDir, toLight);

                if (angleToLight > MaxFlashlightAngle)
                    continue;

                this._lastFlashTime = Time.time;
                this._isBlinded = true;

                BotSuppressionHelper.TrySuppressBot(this._bot.GetPlayer, light.transform.position);
                break;
            }
        }

        /// <summary>
        ///     Computes recovery time based on bot composure.
        ///     Higher composure leads to faster recovery.
        /// </summary>
        private float GetBlindRecoveryTime()
        {
            var composure = this._cache?.PanicHandler?.GetComposureLevel() ?? 1f;
            return Mathf.Lerp(2f, BaseBlindDuration, 1f - composure);
        }
    }
}