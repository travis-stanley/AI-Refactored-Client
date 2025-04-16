#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Reactions
{
    /// <summary>
    /// Detects intense light sources (flashlights, flashbangs) and triggers suppression via BotSuppressionHelper.
    /// </summary>
    public class FlashGrenadeComponent : MonoBehaviour
    {
        public BotOwner? Bot { get; private set; }

        private float _lastFlashTime = -999f;
        private bool _isBlinded = false;

        private const float BlindDuration = 4.5f;
        private const float FlashlightThresholdAngle = 25f;
        private const float FlashlightMinIntensity = 2.0f;

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
        }

        private void Update()
        {
            if (Bot == null || Bot.HealthController == null)
                return;

            CheckFlashlightExposure();

            if (_isBlinded && Time.time - _lastFlashTime > BlindDuration)
            {
                _isBlinded = false;
            }
        }

        private void CheckFlashlightExposure()
        {
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

        public bool IsFlashed()
        {
            return _isBlinded;
        }

        public void AddBlindEffect(float duration, Vector3 source)
        {
            _lastFlashTime = Time.time;
            _isBlinded = true;

            if (Bot?.GetPlayer != null)
            {
                BotSuppressionHelper.TrySuppressBot(Bot.GetPlayer, source);
            }

#if UNITY_EDITOR
            UnityEngine.Debug.Log($"[AIRefactored-Flash] Bot {Bot?.Profile?.Info?.Nickname} is flashed and suppressed.");
#endif
        }
    }
}
