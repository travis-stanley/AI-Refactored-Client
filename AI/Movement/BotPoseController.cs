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
        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly MovementContext _movement;
        private readonly BotPersonalityProfile _personality;

        private float _targetPoseLevel = 100f;
        private float _currentPoseLevel = 100f;

        private float _nextPoseCheck = 0f;
        private const float PoseCheckInterval = 0.3f;

        private float _suppressedUntil = 0f;
        private const float SuppressionCrouchDuration = 2.5f;

        private const float FlankAngleThreshold = 120f;
        private const float PoseBlendSpeedBase = 140f;

        public BotPoseController(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _movement = bot.GetPlayer.MovementContext;
            _personality = _cache.AIRefactoredBotOwner?.PersonalityProfile ?? new BotPersonalityProfile();
        }

        /// <summary>
        /// Ticked from BotBrain. Updates target pose level, and smoothly blends into it.
        /// </summary>
        public void Tick(float time)
        {
            if (time < _nextPoseCheck)
            {
                BlendPose(Time.deltaTime);
                return;
            }

            _nextPoseCheck = time + PoseCheckInterval;
            EvaluatePoseIntent(time);
            BlendPose(Time.deltaTime);
        }

        private void EvaluatePoseIntent(float time)
        {
            // === 1. Suppressed
            if (_cache.Suppression?.IsSuppressed() == true)
                _suppressedUntil = time + SuppressionCrouchDuration;

            if (time < _suppressedUntil)
            {
                _targetPoseLevel = 50f; // crouch
                return;
            }

            // === 2. Panic (fully prone)
            if (_cache.PanicHandler?.IsPanicking == true)
            {
                _targetPoseLevel = 0f;
                return;
            }

            // === 3. Smart prone: flanked
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

            // === 4. Approaching cover (anticipation)
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

            // === 5. Default stance logic
            bool inCombat = _cache.Combat?.IsInCombatState() == true;
            bool crouchPreferred = _personality.Caution > 0.6f || _personality.IsCamper;

            _targetPoseLevel = (inCombat && crouchPreferred) ? 50f : 100f;
        }

        private void BlendPose(float deltaTime)
        {
            float panicFactor = _cache.PanicHandler?.IsPanicking == true ? 0.6f : 1f;
            float combatFactor = _cache.Combat?.IsInCombatState() == true ? 1f : 0.4f;

            float blendSpeed = PoseBlendSpeedBase * panicFactor * combatFactor;
            _currentPoseLevel = Mathf.MoveTowards(_currentPoseLevel, _targetPoseLevel, blendSpeed * deltaTime);
            _movement.SetPoseLevel(_currentPoseLevel);
        }

        // === Public API for external stance requests (e.g. from CombatStateMachine) ===

        public void SetStand()
        {
            _targetPoseLevel = 100f;
        }

        public void SetCrouch(bool anticipate = false)
        {
            _targetPoseLevel = anticipate ? 60f : 50f;
        }

        public void SetProne(bool anticipate = false)
        {
            _targetPoseLevel = anticipate ? 20f : 0f;
        }
    }
}
