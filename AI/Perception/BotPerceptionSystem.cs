#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Core;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Dynamically modifies bot perception (vision range, flash blindness, suppression response).
    /// Connected to BotOwner and LookSensor. Syncs with combat and speech systems via component cache.
    /// </summary>
    public class BotPerceptionSystem : MonoBehaviour, IFlashReactiveBot
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotVisionProfile? _profile;

        private float _flashBlindness = 0f;
        private float _flareIntensity = 0f;
        private float _suppressionFactor = 0f;

        private float _blindStartTime = -1f;

        private const float FlashRecoverySpeed = 0.5f;
        private const float BlindSpeechThreshold = 0.4f;
        private const float PanicTriggerThreshold = 0.6f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();

            if (_bot?.GetPlayer != null)
            {
                _profile = BotVisionProfiles.Get(_bot.GetPlayer);
            }
        }

        private void Update()
        {
            if (_bot == null || _profile == null || _bot.IsDead || _cache == null)
                return;

            // 🛑 Skip for any human-controlled player or FIKA coop players
            if (_bot.GetPlayer != null && _bot.GetPlayer.IsYourPlayer)
                return;

            float penalty = Mathf.Max(_flashBlindness, _flareIntensity, _suppressionFactor);
            float baseRange = Mathf.Lerp(15f, 70f, 1f - penalty);

            _bot.LookSensor.ClearVisibleDist = baseRange * _profile.AdaptationSpeed;

            _cache.IsBlinded = _flashBlindness > 0.3f;
            _cache.BlindUntilTime = Time.time + Mathf.Clamp01(_flashBlindness) * 3f;

            TryTriggerPanic();
            RecoverPerception();
        }

        #endregion

        #region Perception Modifiers

        public void ApplyFlashBlindness(float intensity)
        {
            if (_profile == null || _cache == null)
                return;

            // 🛑 Never apply to FIKA/human players
            if (_bot?.GetPlayer != null && _bot.GetPlayer.IsYourPlayer)
                return;

            _flashBlindness = Mathf.Clamp(_flashBlindness + intensity * _profile.MaxBlindness, 0f, 1f);
            _cache.LastFlashTime = Time.time;

            if (_flashBlindness > BlindSpeechThreshold)
                _bot?.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);

            _blindStartTime = Time.time;
        }

        public void ApplyFlareExposure(float strength)
        {
            if (_bot?.GetPlayer != null && _bot.GetPlayer.IsYourPlayer)
                return;

            _flareIntensity = Mathf.Clamp(strength * 0.6f, 0f, 0.8f);
        }

        public void ApplySuppression(float severity)
        {
            if (_profile == null)
                return;

            if (_bot?.GetPlayer != null && _bot.GetPlayer.IsYourPlayer)
                return;

            _suppressionFactor = Mathf.Clamp(severity * _profile.AggressionResponse, 0f, 1f);
        }

        public void OnFlashExposure(Vector3 lightOrigin)
        {
            if (_bot?.GetPlayer != null && _bot.GetPlayer.IsYourPlayer)
                return;

            ApplyFlashBlindness(0.4f);
        }

        #endregion

        #region Panic and Recovery

        private void RecoverPerception()
        {
            _flashBlindness = Mathf.MoveTowards(_flashBlindness, 0f, FlashRecoverySpeed * Time.deltaTime);
            _flareIntensity = Mathf.MoveTowards(_flareIntensity, 0f, 0.25f * Time.deltaTime);
            _suppressionFactor = Mathf.MoveTowards(_suppressionFactor, 0f, 0.3f * Time.deltaTime);
        }

        private void TryTriggerPanic()
        {
            if (_cache?.PanicHandler == null || _bot == null)
                return;

            if (_flashBlindness >= PanicTriggerThreshold && Time.time - _blindStartTime < 2.5f)
            {
                _cache.PanicHandler.TriggerPanic();
            }
        }

        #endregion
    }
}
