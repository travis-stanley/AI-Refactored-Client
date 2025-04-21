#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Modifies bot visual perception based on flashbangs, flashlights, flares, and suppression exposure.
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

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;
        private static readonly bool DebugEnabled = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the perception system with references to the bot and its vision profile.
        /// </summary>
        /// <param name="cache">Bot component cache reference.</param>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;

            if (_bot?.GetPlayer == null)
            {
                _profile = null;
                return;
            }

            _profile = BotVisionProfiles.Get(_bot.GetPlayer);
        }

        #endregion

        #region Tick Loop

        /// <summary>
        /// Called by BotBrain.Tick to update current perception modifiers and handle reactive behaviors.
        /// </summary>
        /// <param name="deltaTime">Delta time since last tick.</param>
        public void Tick(float deltaTime)
        {
            if (_bot == null || _cache == null || _profile == null || _bot.IsDead)
                return;

            var player = _bot.GetPlayer;
            if (player == null || player.IsYourPlayer)
                return;

            // Flashlight exposure detection
            Transform? head = BotCacheUtility.Head(_cache);
            if (head != null && FlashlightRegistry.IsExposingBot(head, out _))
            {
                ApplyFlashBlindness(0.25f);
            }

            float penalty = Mathf.Max(_flashBlindness, _flareIntensity, _suppressionFactor);
            float baseRange = Mathf.Lerp(15f, 70f, 1f - penalty);

            _bot.LookSensor.ClearVisibleDist = baseRange * _profile.AdaptationSpeed;
            _cache.IsBlinded = _flashBlindness > 0.3f;
            _cache.BlindUntilTime = Time.time + Mathf.Clamp01(_flashBlindness) * 3f;

            if (DebugEnabled)
            {
                string name = _bot.Profile?.Info?.Nickname ?? "bot";
                Logger.LogDebug($"[Perception] {name} penalty={penalty:F2}, blind={_cache.IsBlinded}, range={_bot.LookSensor.ClearVisibleDist:F1}");
            }

            TryTriggerPanic();
            RecoverPerception(deltaTime);
            TryShareEnemy();
        }

        #endregion

        #region Reactive Modifiers

        /// <summary>
        /// Increases flash blindness based on incoming flashlight or flashbang exposure.
        /// </summary>
        /// <param name="intensity">Flash intensity to apply (0-1 scale).</param>
        public void ApplyFlashBlindness(float intensity)
        {
            if (_profile == null || _cache == null || _bot?.GetPlayer?.IsYourPlayer == true)
                return;

            _flashBlindness = Mathf.Clamp(_flashBlindness + intensity * _profile.MaxBlindness, 0f, 1f);
            _cache.LastFlashTime = Time.time;
            _blindStartTime = Time.time;

            if (DebugEnabled)
                Logger.LogDebug($"[Perception] FlashBlindness now {_flashBlindness:F2}");

            if (_flashBlindness > BlindSpeechThreshold)
            {
                _bot?.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
            }
        }

        /// <summary>
        /// Applies temporary visual suppression from flares.
        /// </summary>
        /// <param name="strength">Flare light strength as a 0-1 factor.</param>
        public void ApplyFlareExposure(float strength)
        {
            if (_bot?.GetPlayer?.IsYourPlayer == true)
                return;

            _flareIntensity = Mathf.Clamp(strength * 0.6f, 0f, 0.8f);
        }

        /// <summary>
        /// Applies suppression modifier from gunfire or sustained threat.
        /// </summary>
        /// <param name="severity">Suppression severity value (0-1).</param>
        public void ApplySuppression(float severity)
        {
            if (_profile == null || _bot?.GetPlayer?.IsYourPlayer == true)
                return;

            _suppressionFactor = Mathf.Clamp(severity * _profile.AggressionResponse, 0f, 1f);

            if (DebugEnabled)
                Logger.LogDebug($"[Perception] SuppressionFactor = {_suppressionFactor:F2}");
        }

        /// <summary>
        /// Handler for unified flash exposure events (used by grenades and global sources).
        /// </summary>
        /// <param name="lightOrigin">Origin point of light burst.</param>
        public void OnFlashExposure(Vector3 lightOrigin)
        {
            if (_bot?.GetPlayer?.IsYourPlayer == true)
                return;

            ApplyFlashBlindness(0.4f);
        }

        #endregion

        #region Internal Logic

        /// <summary>
        /// Gradually recovers from flash, flare, and suppression exposure over time.
        /// </summary>
        /// <param name="deltaTime">Time step for decay.</param>
        private void RecoverPerception(float deltaTime)
        {
            _flashBlindness = Mathf.MoveTowards(_flashBlindness, 0f, FlashRecoverySpeed * deltaTime);
            _flareIntensity = Mathf.MoveTowards(_flareIntensity, 0f, 0.25f * deltaTime);
            _suppressionFactor = Mathf.MoveTowards(_suppressionFactor, 0f, 0.3f * deltaTime);
        }

        /// <summary>
        /// Triggers panic state if flash blindness crosses threshold during early recovery period.
        /// </summary>
        private void TryTriggerPanic()
        {
            if (_cache?.PanicHandler == null || _bot == null)
                return;

            if (_flashBlindness >= PanicTriggerThreshold && Time.time - _blindStartTime < 2.5f)
            {
                _cache.PanicHandler.TriggerPanic();

                if (DebugEnabled)
                {
                    string name = _bot.Profile?.Info?.Nickname ?? "bot";
                    Logger.LogDebug($"[Perception] Panic triggered by blindness on {name}");
                }
            }
        }

        /// <summary>
        /// Shares target knowledge with squadmates if enemy is seen and valid.
        /// </summary>
        private void TryShareEnemy()
        {
            if (_bot == null)
                return;

            var enemy = _bot.Memory?.GoalEnemy?.Person;
            if (enemy != null)
            {
                BotTeamLogic.AddEnemy(_bot, enemy);
            }
        }

        #endregion
    }
}
