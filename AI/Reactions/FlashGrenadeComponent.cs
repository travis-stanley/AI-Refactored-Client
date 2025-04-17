#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Detects intense directional light sources (e.g. flashlights, flashbangs) and simulates temporary blindness.
    /// Applies suppression and panic responses based on exposure severity and angle.
    /// </summary>
    public class FlashGrenadeComponent : MonoBehaviour
    {
        #region Public Properties

        /// <summary>
        /// Reference to the associated BotOwner component.
        /// </summary>
        public BotOwner? Bot { get; private set; }

        #endregion

        #region Private Fields

        private float _lastFlashTime = -999f;
        private bool _isBlinded = false;

        private const float BlindDuration = 4.5f;
        private const float FlashlightThresholdAngle = 25f;
        private const float FlashlightMinIntensity = 2.0f;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Called during MonoBehaviour initialization. Caches BotOwner reference.
        /// </summary>
        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
        }

        /// <summary>
        /// Evaluates flashlight exposure and updates blindness status every frame.
        /// </summary>
        private void Update()
        {
            if (Bot == null || Bot.HealthController == null || !Bot.GetPlayer?.IsAI == true)
                return;

            CheckFlashlightExposure();

            if (_isBlinded && Time.time - _lastFlashTime > BlindDuration)
                _isBlinded = false;
        }

        #endregion

        #region Flashlight Detection

        /// <summary>
        /// Scans the environment for active spotlights simulating flashlights.
        /// If bot is facing an intense beam, triggers blindness and suppression logic.
        /// </summary>
        private void CheckFlashlightExposure()
        {
            if (Bot == null || Bot.IsDead || Bot.Transform == null)
                return;

            Vector3 botForward = Bot.LookDirection;
            Vector3 botPosition = Bot.Transform.position;

            foreach (Light light in GameObject.FindObjectsOfType<Light>())
            {
                if (!light.enabled || light.type != LightType.Spot || light.intensity < FlashlightMinIntensity)
                    continue;

                Vector3 dirToLight = (light.transform.position - botPosition).normalized;
                float angle = Vector3.Angle(botForward, -dirToLight);

                if (angle < FlashlightThresholdAngle)
                {
                    AddBlindEffect(BlindDuration, light.transform.position);
                    break;
                }
            }
        }

        #endregion

        #region Flash Reaction Logic

        /// <summary>
        /// Returns whether the bot is currently blinded by a light source.
        /// </summary>
        public bool IsFlashed() => _isBlinded;

        /// <summary>
        /// Triggers temporary blindness and suppresses bot behavior from a light source.
        /// </summary>
        /// <param name="duration">Blindness duration in seconds.</param>
        /// <param name="source">World-space origin of the flash effect.</param>
        public void AddBlindEffect(float duration, Vector3 source)
        {
            if (Bot == null || !Bot.GetPlayer?.IsAI == true)
                return;

            _lastFlashTime = Time.time;
            _isBlinded = true;

            BotSuppressionHelper.TrySuppressBot(Bot.GetPlayer, source);
        }

        #endregion
    }
}
