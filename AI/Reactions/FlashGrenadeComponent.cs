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
        /// Reference to the associated bot's owner.
        /// </summary>
        public BotOwner? Bot { get; private set; }

        private BotComponentCache? _cache;

        #endregion

        #region Private Fields

        private float _lastFlashTime = -999f;
        private bool _isBlinded = false;

        private const float BlindDuration = 4.5f;
        private const float FlashlightThresholdAngle = 25f;
        private const float FlashlightMinIntensity = 2.0f;

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;
        private static readonly bool _debug = false;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            Bot = cache.Bot;
        }

        #endregion

        #region External Tick

        public void Tick(float time)
        {
            if (Bot == null || Bot.HealthController == null || !Bot.GetPlayer?.IsAI == true)
                return;

            CheckFlashlightExposure();

            if (_isBlinded && time - _lastFlashTime > BlindDuration)
            {
                if (_debug)
                    _log.LogDebug($"[FlashGrenadeComponent] Bot {Bot.Profile?.Id} recovered from blind.");
                _isBlinded = false;
            }
        }

        #endregion

        #region Flashlight Detection

        private void CheckFlashlightExposure()
        {
            if (Bot == null || Bot.IsDead || Bot.Transform == null)
                return;

            Vector3 botForward = Bot.LookDirection;
            Vector3 botPosition = Bot.Transform.position;

            foreach (Light light in FlashlightRegistry.GetActiveFlashlights())
            {
                if (light == null || !light.enabled || light.intensity < FlashlightMinIntensity)
                    continue;

                Vector3 dirToLight = (light.transform.position - botPosition).normalized;
                float angle = Vector3.Angle(botForward, -dirToLight);

                if (angle < FlashlightThresholdAngle)
                {
                    if (_debug)
                        _log.LogDebug($"[FlashGrenadeComponent] Bot {Bot.Profile?.Id} blinded by flashlight at angle {angle:0.0}°");

                    AddBlindEffect(BlindDuration, light.transform.position);
                    break;
                }
            }
        }

        #endregion

        #region Flash Reaction Logic

        public bool IsFlashed() => _isBlinded;

        public void AddBlindEffect(float duration, Vector3 source)
        {
            if (Bot == null || !Bot.GetPlayer?.IsAI == true)
                return;

            _lastFlashTime = Time.time;
            _isBlinded = true;

            if (_debug)
                _log.LogDebug($"[FlashGrenadeComponent] Blind effect applied to {Bot.Profile?.Id} for {duration:0.00}s");

            BotSuppressionHelper.TrySuppressBot(Bot.GetPlayer, source);
        }

        #endregion
    }
}
