#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Controls visual impairment from flashbangs, flares, and suppression.
    /// Adjusts sight distance, triggers panic and vocalizations, and syncs team awareness.
    /// </summary>
    public sealed class BotPerceptionSystem : IFlashReactiveBot
    {
        #region Constants

        private const float FlashRecoverySpeed = 0.5f;
        private const float FlareRecoverySpeed = 0.2f;
        private const float SuppressionRecoverySpeed = 0.3f;

        private const float BlindSpeechThreshold = 0.4f;
        private const float PanicTriggerThreshold = 0.6f;

        private const float MinSightDistance = 15f;
        private const float MaxSightDistance = 70f;

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotVisionProfile? _profile;

        private float _flashBlindness;
        private float _flareIntensity;
        private float _suppressionFactor;
        private float _blindStartTime = -1f;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;

            if (_bot?.GetPlayer is { IsAI: true } player)
                _profile = BotVisionProfiles.Get(player);
        }

        #endregion

        #region Tick

        public void Tick(float deltaTime)
        {
            if (!IsValid())
                return;

            HandleFlashlightExposure();

            float perceptionPenalty = Mathf.Max(_flashBlindness, _flareIntensity, _suppressionFactor);
            float adjustedRange = Mathf.Lerp(MinSightDistance, MaxSightDistance, 1f - perceptionPenalty);

            if (_profile != null)
                _bot!.LookSensor.ClearVisibleDist = adjustedRange * _profile.AdaptationSpeed;

            bool isBlinded = _flashBlindness > BlindSpeechThreshold;
            if (_cache != null)
            {
                _cache.IsBlinded = isBlinded;
                _cache.BlindUntilTime = Time.time + Mathf.Clamp01(_flashBlindness) * 3f;
            }

            TryTriggerPanic();
            RecoverVisualClarity(deltaTime);
            SyncEnemyIfVisible();
        }

        #endregion

        #region Exposure Handlers

        public void OnFlashExposure(Vector3 lightOrigin)
        {
            if (!IsValid())
                return;

            ApplyFlashBlindness(0.4f);
        }

        public void ApplyFlashBlindness(float intensity)
        {
            if (!IsValid() || _profile == null)
                return;

            _flashBlindness = Mathf.Clamp01(_flashBlindness + intensity * _profile.MaxBlindness);
            _blindStartTime = Time.time;

            if (_flashBlindness > BlindSpeechThreshold)
                _bot?.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
        }

        public void ApplyFlareExposure(float strength)
        {
            _flareIntensity = Mathf.Clamp(strength * 0.6f, 0f, 0.8f);
        }

        public void ApplySuppression(float severity)
        {
            if (!IsValid() || _profile == null)
                return;

            _suppressionFactor = Mathf.Clamp01(severity * _profile.AggressionResponse);
        }

        #endregion

        #region Internals

        private void RecoverVisualClarity(float deltaTime)
        {
            _flashBlindness = Mathf.MoveTowards(_flashBlindness, 0f, FlashRecoverySpeed * deltaTime);
            _flareIntensity = Mathf.MoveTowards(_flareIntensity, 0f, FlareRecoverySpeed * deltaTime);
            _suppressionFactor = Mathf.MoveTowards(_suppressionFactor, 0f, SuppressionRecoverySpeed * deltaTime);
        }

        private void TryTriggerPanic()
        {
            if (_cache?.PanicHandler == null || _bot == null)
                return;

            if (_flashBlindness >= PanicTriggerThreshold &&
                (Time.time - _blindStartTime) < 2.5f)
            {
                _cache.PanicHandler.TriggerPanic();
            }
        }

        private void HandleFlashlightExposure()
        {
            if (_cache == null)
                return;

            var head = BotCacheUtility.Head(_cache);
            if (head != null && FlashlightRegistry.IsExposingBot(head, out _))
                ApplyFlashBlindness(0.25f);
        }

        private void SyncEnemyIfVisible()
        {
            if (_bot == null || _cache == null || _cache.IsBlinded)
                return;

            var enemy = _bot.Memory?.GoalEnemy?.Person;
            if (enemy != null)
                BotTeamLogic.AddEnemy(_bot, enemy);
        }

        private bool IsValid()
        {
            return _bot is { IsDead: false } &&
                   _cache != null &&
                   _profile != null &&
                   _bot.GetPlayer is { IsAI: true };
        }

        #endregion
    }
}
