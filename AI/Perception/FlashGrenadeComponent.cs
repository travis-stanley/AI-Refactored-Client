#nullable enable

using UnityEngine;
using EFT;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Detects if the bot is blinded by a flashlight or flash-based effect.
    /// Tracks angle and intensity, triggers timed blindness.
    /// </summary>
    public class FlashGrenadeComponent : MonoBehaviour
    {
        public BotOwner Bot { get; private set; } = null!;

        private float lastFlashTime;
        private bool isBlinded;
        private const float BlindDuration = 4.5f;

        private static readonly float FlashlightThresholdAngle = 25f;
        private static readonly float FlashlightMinIntensity = 2.0f;

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
        }

        private void Update()
        {
            if (Bot == null || Bot.HealthController == null)
                return;

            CheckFlashlightExposure();

            if (isBlinded && Time.time - lastFlashTime > BlindDuration)
            {
                isBlinded = false;
            }
        }

        private void CheckFlashlightExposure()
        {
            Vector3 botForward = Bot.LookDirection;
            Vector3 botPosition = Bot.Position;

            Light[] lights = FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (!light.enabled || light.type != LightType.Spot || light.intensity < FlashlightMinIntensity)
                    continue;

                Vector3 dirToLight = (light.transform.position - botPosition).normalized;
                float angle = Vector3.Angle(botForward, -dirToLight);

                if (angle < FlashlightThresholdAngle)
                {
                    AddBlindEffect(BlindDuration);
                    break;
                }
            }
        }

        /// <summary>
        /// Returns true if bot is currently blinded.
        /// </summary>
        public bool IsFlashed()
        {
            return isBlinded;
        }

        /// <summary>
        /// Applies blindness effect for X seconds.
        /// </summary>
        public void AddBlindEffect(float duration)
        {
            lastFlashTime = Time.time;
            isBlinded = true;

            Debug.Log($"[AIRefactored] Bot {Bot?.Profile?.Info?.Nickname ?? "?"} is blinded for {duration} seconds.");
        }
    }
}
