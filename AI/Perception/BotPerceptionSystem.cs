#nullable enable
using UnityEngine;
using EFT;


namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Dynamically modifies bot perception (vision range, flash blindness, suppression response).
    /// Plugged directly into BotOwner and LookSensor.
    /// </summary>
    public class BotPerceptionSystem : MonoBehaviour, IFlashReactiveBot
    {
        private BotOwner? _bot;
        private BotVisionProfile? _profile;

        private float _flashBlindness = 0f;
        private float _flareIntensity = 0f;
        private float _suppressionFactor = 0f;

        private const float FlashRecoverySpeed = 0.5f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            if (_bot?.GetPlayer != null)
            {
                _profile = BotVisionProfiles.Get(_bot.GetPlayer);
            }
        }

        private void Update()
        {
            if (_bot == null || _profile == null || _bot.IsDead)
                return;

            float penalty = Mathf.Max(_flashBlindness, _flareIntensity, _suppressionFactor);
            float baseRange = Mathf.Lerp(15f, 70f, 1f - penalty);

            _bot.LookSensor.ClearVisibleDist = baseRange * _profile.AdaptationSpeed;

            RecoverFromBlindness();
        }

        public void ApplyFlashBlindness(float intensity)
        {
            if (_profile == null) return;

            _flashBlindness = Mathf.Clamp(
                _flashBlindness + intensity * _profile.MaxBlindness,
                0f, 1f
            );
        }

        public void ApplyFlareExposure(float strength)
        {
            _flareIntensity = Mathf.Clamp(strength * 0.6f, 0f, 0.8f);
        }

        public void ApplySuppression(float severity)
        {
            if (_profile == null) return;

            _suppressionFactor = Mathf.Clamp(
                severity * _profile.AggressionResponse,
                0f, 1f
            );
        }

        public void OnFlashExposure(Vector3 lightOrigin)
        {
            ApplyFlashBlindness(0.4f);
        }

        private void RecoverFromBlindness()
        {
            _flashBlindness = Mathf.MoveTowards(_flashBlindness, 0f, FlashRecoverySpeed * Time.deltaTime);
            _flareIntensity = Mathf.MoveTowards(_flareIntensity, 0f, 0.25f * Time.deltaTime);
            _suppressionFactor = Mathf.MoveTowards(_suppressionFactor, 0f, 0.3f * Time.deltaTime);
        }
    }
}
