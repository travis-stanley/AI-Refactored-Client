#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Navigation;
using EFT;
using System;
using UnityEngine;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Controls bot stance transitions (standing, crouching, prone) based on combat, panic, suppression, and cover logic.
    /// Smoothly blends transitions for realistic animation flow.
    /// </summary>
    public class BotPoseController
    {
        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly MovementContext _movement;
        private readonly BotPersonalityProfile _personality;

        private float _targetPoseLevel;
        private float _currentPoseLevel;
        private float _nextPoseCheck;
        private float _suppressedUntil;
        private bool _isPoseLocked;

        private const float PoseCheckInterval = 0.3f;
        private const float SuppressionCrouchDuration = 2.5f;
        private const float FlankAngleThreshold = 120f;
        private const float PoseBlendSpeedBase = 140f;
        private const float MinPoseThreshold = 0.1f;

        #endregion

        #region Constructor

        public BotPoseController(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _movement = bot.GetPlayer?.MovementContext ?? throw new InvalidOperationException("Missing MovementContext.");
            _personality = cache.AIRefactoredBotOwner?.PersonalityProfile ?? throw new InvalidOperationException("Missing PersonalityProfile.");

            _currentPoseLevel = _movement.PoseLevel;
            _targetPoseLevel = _currentPoseLevel;
            _nextPoseCheck = Time.time;
        }

        #endregion

        #region Public API

        public float GetPoseLevel() => _movement.PoseLevel;

        public void Tick(float currentTime)
        {
            if (_bot.IsDead)
                return;

            if (_isPoseLocked)
            {
                BlendPose(Time.deltaTime);
                return;
            }

            if (currentTime >= _nextPoseCheck)
            {
                _nextPoseCheck = currentTime + PoseCheckInterval;
                EvaluatePoseIntent(currentTime);
            }

            BlendPose(Time.deltaTime);
        }

        public void SetStand() => _targetPoseLevel = 100f;
        public void SetCrouch(bool anticipate = false) => _targetPoseLevel = anticipate ? 60f : 50f;
        public void SetProne(bool anticipate = false) => _targetPoseLevel = anticipate ? 20f : 0f;

        public void LockCrouchPose()
        {
            _targetPoseLevel = 50f;
            _isPoseLocked = true;
        }

        public void UnlockPose() => _isPoseLocked = false;

        /// <summary>
        /// Sets pose level based on nearby cover (uses NavPointRegistry scan).
        /// </summary>
        public void TrySetStanceFromNearbyCover(Vector3 position)
        {
            var nearbyPoints = NavPointRegistry.QueryNearby(position, 4f, point =>
            {
                float distSq = (point - position).sqrMagnitude;
                return distSq <= 16f && (
                    BotCoverHelper.IsProneCover(point) ||
                    BotCoverHelper.IsLowCover(point)
                );
            });

            foreach (var point in nearbyPoints)
            {
                if (BotCoverHelper.IsProneCover(point))
                {
                    SetProne(true);
                }
                else if (BotCoverHelper.IsLowCover(point))
                {
                    SetCrouch(true);
                }
                else
                {
                    SetStand();
                }

                break;
            }
        }

        #endregion

        #region Pose Evaluation

        private void EvaluatePoseIntent(float currentTime)
        {
            if (_cache.PanicHandler?.IsPanicking == true)
            {
                _targetPoseLevel = 0f;
                return;
            }

            if (_cache.Suppression?.IsSuppressed() == true)
                _suppressedUntil = currentTime + SuppressionCrouchDuration;

            if (currentTime < _suppressedUntil)
            {
                _targetPoseLevel = 50f;
                return;
            }

            if (_personality.IsFrenzied || _personality.IsFearful || _personality.Personality == PersonalityType.Sniper)
            {
                if (BotMemoryExtensions.TryGetFlankDirection(_bot) is Vector3 flankDir)
                {
                    float flankAngle = Vector3.Angle(_bot.LookDirection, flankDir.normalized);
                    if (flankAngle > FlankAngleThreshold)
                    {
                        _targetPoseLevel = 0f;
                        return;
                    }
                }
            }

            var maybeCover = _bot.Memory?.BotCurrentCoverInfo?.LastCover;
            if (maybeCover != null)
            {
                Vector3 coverPos = maybeCover.Position;
                float dist = Vector3.Distance(_bot.Position, coverPos);

                if (dist < 2.5f)
                {
                    if (BotCoverHelper.IsProneCover(maybeCover))
                    {
                        _targetPoseLevel = 0f;
                        return;
                    }

                    if (BotCoverHelper.IsLowCover(maybeCover))
                    {
                        _targetPoseLevel = 50f;
                        return;
                    }
                }
            }

            bool inCombat = _cache.Combat?.IsInCombatState() == true;
            bool preferCrouch = _personality.Caution > 0.6f || _personality.IsCamper;

            _targetPoseLevel = (inCombat && preferCrouch) ? 50f : 100f;
        }

        #endregion

        #region Pose Transition

        private void BlendPose(float deltaTime)
        {
            if (Mathf.Abs(_currentPoseLevel - _targetPoseLevel) < MinPoseThreshold)
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
