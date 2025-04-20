#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Modifies bot visual perception based on flashbangs, flares, and suppression exposure.
    /// Dynamically adjusts visible distance and tracks blindness states.
    /// </summary>
    public class BotPerceptionSystem : IFlashReactiveBot
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

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;
        private static readonly bool _debug = false;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
            _profile = BotVisionProfiles.Get(_bot.GetPlayer);
        }

        #endregion

        #region Tick Loop

        /// <summary>
        /// Called every frame by BotBrain to apply recovery and perception penalties.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_bot == null || _profile == null || _cache == null || _bot.IsDead)
                return;

            var player = _bot.GetPlayer;
            if (player == null || player.IsYourPlayer)
                return;

            float penalty = Mathf.Max(_flashBlindness, _flareIntensity, _suppressionFactor);
            float baseRange = Mathf.Lerp(15f, 70f, 1f - penalty);

            _bot.LookSensor.ClearVisibleDist = baseRange * _profile.AdaptationSpeed;
            _cache.IsBlinded = _flashBlindness > 0.3f;
            _cache.BlindUntilTime = Time.time + Mathf.Clamp01(_flashBlindness) * 3f;

            if (_debug)
                _log.LogDebug($"[Perception] Penalty: {penalty:F2}, Blind: {_cache.IsBlinded}, Range: {_bot.LookSensor.ClearVisibleDist:F1}");

            TryTriggerPanic();
            RecoverPerception(deltaTime);
            TryShareEnemy();
        }

        #endregion

        #region Reactive Modifiers

        public void ApplyFlashBlindness(float intensity)
        {
            if (_profile == null || _cache == null || _bot?.GetPlayer?.IsYourPlayer == true)
                return;

            _flashBlindness = Mathf.Clamp(_flashBlindness + intensity * _profile.MaxBlindness, 0f, 1f);
            _cache.LastFlashTime = Time.time;
            _blindStartTime = Time.time;

            if (_debug)
                _log.LogDebug($"[Perception] FlashBlindness: {_flashBlindness:F2} after exposure");

            if (_flashBlindness > BlindSpeechThreshold)
                _bot?.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
        }

        public void ApplyFlareExposure(float strength)
        {
            if (_bot?.GetPlayer?.IsYourPlayer == true)
                return;

            _flareIntensity = Mathf.Clamp(strength * 0.6f, 0f, 0.8f);
        }

        public void ApplySuppression(float severity)
        {
            if (_profile == null || _bot?.GetPlayer?.IsYourPlayer == true)
                return;

            _suppressionFactor = Mathf.Clamp(severity * _profile.AggressionResponse, 0f, 1f);

            if (_debug)
                _log.LogDebug($"[Perception] SuppressionFactor updated to {_suppressionFactor:F2}");
        }

        public void OnFlashExposure(Vector3 lightOrigin)
        {
            if (_bot?.GetPlayer?.IsYourPlayer == true)
                return;

            ApplyFlashBlindness(0.4f);
        }

        #endregion

        #region Internals

        private void RecoverPerception(float deltaTime)
        {
            _flashBlindness = Mathf.MoveTowards(_flashBlindness, 0f, FlashRecoverySpeed * deltaTime);
            _flareIntensity = Mathf.MoveTowards(_flareIntensity, 0f, 0.25f * deltaTime);
            _suppressionFactor = Mathf.MoveTowards(_suppressionFactor, 0f, 0.3f * deltaTime);
        }

        private void TryTriggerPanic()
        {
            if (_cache?.PanicHandler == null || _bot == null)
                return;

            if (_flashBlindness >= PanicTriggerThreshold && Time.time - _blindStartTime < 2.5f)
            {
                _cache.PanicHandler.TriggerPanic();

                if (_debug)
                    _log.LogDebug($"[Perception] Panic triggered by flash blindness on {_bot.Profile?.Info?.Nickname}");
            }
        }

        private void TryShareEnemy()
        {
            if (_bot?.Memory?.GoalEnemy?.Person != null)
            {
                BotTeamLogic.AddEnemy(_bot, _bot.Memory.GoalEnemy.Person);
            }
        }

        #endregion
    }
}
