#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Detects intense directional light sources (flashlights, flashbangs) and simulates temporary blindness.
    /// Applies panic and suppression reactions when overexposed.
    /// </summary>
    public class FlashGrenadeComponent : MonoBehaviour
    {
        #region Fields

        public BotOwner? Bot { get; private set; }

        private float _lastFlashTime = -999f;
        private bool _isBlinded = false;

        private const float BlindDuration = 4.5f;
        private const float FlashlightThresholdAngle = 25f;
        private const float FlashlightMinIntensity = 2.0f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
        }

        private void Update()
        {
            if (Bot == null || Bot.HealthController == null || !Bot.GetPlayer?.IsAI == true)
                return;

            CheckFlashlightExposure();

            if (_isBlinded && Time.time - _lastFlashTime > BlindDuration)
            {
                _isBlinded = false;
            }
        }

        #endregion

        #region Flashlight Detection

        /// <summary>
        /// Scans all active lights to determine if bot is facing a bright spotlight.
        /// </summary>
        private void CheckFlashlightExposure()
        {
            if (Bot == null || Bot.IsDead || Bot.Transform == null)
                return;

            Vector3 botForward = Bot.LookDirection;
            Vector3 botPosition = Bot.Transform.position;

            foreach (var light in GameObject.FindObjectsOfType<Light>())
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
        /// Whether the bot is currently considered blinded.
        /// </summary>
        public bool IsFlashed()
        {
            return _isBlinded;
        }

        /// <summary>
        /// Applies blind effect and triggers panic/suppression logic.
        /// </summary>
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
