#nullable enable

using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

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

        #region Lifecycle

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
        }

        #endregion

        #region External Tick

        public void Tick(float time)
        {
            if (Bot == null || Bot.HealthController == null || !Bot.GetPlayer?.IsAI == true)
                return;

            CheckFlashlightExposure();

            if (_isBlinded && time - _lastFlashTime > BlindDuration)
                _isBlinded = false;
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

        public bool IsFlashed() => _isBlinded;

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
