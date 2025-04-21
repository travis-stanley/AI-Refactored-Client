#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Controls crouching, standing, prone, and lean pose transitions based on combat state, suppression, panic, and cover.
    /// Works in tandem with BotMovementController (which handles leaning).
    /// </summary>
    public class BotPoseController
    {
        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly MovementContext _movement;
        private readonly BotPersonalityProfile _personality;

        private float _targetPoseLevel = 100f;
        private float _currentPoseLevel = 100f;
        private float _nextPoseCheck = 0f;

        private float _suppressedUntil = 0f;

        private const float PoseCheckInterval = 0.3f;
        private const float SuppressionCrouchDuration = 2.5f;
        private const float FlankAngleThreshold = 120f;
        private const float PoseBlendSpeedBase = 140f;

        #endregion

        #region Constructor

        public BotPoseController(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _movement = bot.GetPlayer?.MovementContext ?? throw new System.ArgumentNullException(nameof(bot.GetPlayer));
            _personality = cache.AIRefactoredBotOwner?.PersonalityProfile ?? new BotPersonalityProfile();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Evaluates and blends bot pose based on suppression, panic, combat, and cover state.
        /// </summary>
        public void Tick(float time)
        {
            if (_bot.IsDead)
                return;

            if (time < _nextPoseCheck)
            {
                BlendPose(Time.deltaTime);
                return;
            }

            _nextPoseCheck = time + PoseCheckInterval;
            EvaluatePoseIntent(time);
            BlendPose(Time.deltaTime);
        }

        /// <summary>
        /// Forces standing pose.
        /// </summary>
        public void SetStand() => _targetPoseLevel = 100f;

        /// <summary>
        /// Forces crouch pose.
        /// </summary>
        public void SetCrouch(bool anticipate = false) => _targetPoseLevel = anticipate ? 60f : 50f;

        /// <summary>
        /// Forces prone pose.
        /// </summary>
        public void SetProne(bool anticipate = false) => _targetPoseLevel = anticipate ? 20f : 0f;

        #endregion

        #region Internal Logic

        private void EvaluatePoseIntent(float time)
        {
            // Suppressed bots crouch immediately
            if (_cache.Suppression?.IsSuppressed() == true)
                _suppressedUntil = time + SuppressionCrouchDuration;

            if (time < _suppressedUntil)
            {
                _targetPoseLevel = 50f;
                return;
            }

            // Panic overrides all
            if (_cache.PanicHandler?.IsPanicking == true)
            {
                _targetPoseLevel = 0f;
                return;
            }

            // Flank prone for cautious types
            if (_personality.IsFearful || _personality.Personality == PersonalityType.Sniper || _personality.IsFrenzied)
            {
                Vector3? flankDir = BotMemoryExtensions.TryGetFlankDirection(_bot);
                if (flankDir.HasValue)
                {
                    float angle = Vector3.Angle(_bot.LookDirection, flankDir.Value.normalized);
                    if (angle > FlankAngleThreshold)
                    {
                        _targetPoseLevel = 0f;
                        return;
                    }
                }
            }

            // React to cover context
            var cover = _bot.Memory?.BotCurrentCoverInfo?.LastCover;
            if (cover != null)
            {
                float dist = Vector3.Distance(_bot.Position, cover.Position);
                if (dist < 2.5f)
                {
                    if (BotCoverHelper.IsProneCover(cover))
                    {
                        _targetPoseLevel = 0f;
                        return;
                    }

                    if (BotCoverHelper.IsLowCover(cover))
                    {
                        _targetPoseLevel = 50f;
                        return;
                    }
                }
            }

            // Fallback logic: combat + cautious => crouch
            bool inCombat = _cache.Combat?.IsInCombatState() == true;
            bool crouchPreferred = _personality.Caution > 0.6f || _personality.IsCamper;

            _targetPoseLevel = (inCombat && crouchPreferred) ? 50f : 100f;
        }

        private void BlendPose(float deltaTime)
        {
            if (Mathf.Abs(_currentPoseLevel - _targetPoseLevel) < 0.1f)
                return;

            float panicFactor = _cache.PanicHandler?.IsPanicking == true ? 0.6f : 1f;
            float combatFactor = _cache.Combat?.IsInCombatState() == true ? 1f : 0.4f;

            float blendSpeed = PoseBlendSpeedBase * panicFactor * combatFactor;
            _currentPoseLevel = Mathf.MoveTowards(_currentPoseLevel, _targetPoseLevel, blendSpeed * deltaTime);
            _movement.SetPoseLevel(_currentPoseLevel);
        }

        #endregion
    }
}
