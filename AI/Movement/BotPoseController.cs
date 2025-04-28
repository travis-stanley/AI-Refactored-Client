#nullable enable

namespace AIRefactored.AI.Movement
{
    using System;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Memory;
    using AIRefactored.AI.Navigation;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Controls bot stance transitions (standing, crouching, prone) based on combat, panic, suppression, and cover logic.
    ///     Smoothly blends transitions for realistic animation flow.
    /// </summary>
    public class BotPoseController
    {
        private const float FlankAngleThreshold = 120f;

        private const float MinPoseThreshold = 0.1f;

        private const float PoseBlendSpeedBase = 140f;

        private const float PoseCheckInterval = 0.3f;

        private const float SuppressionCrouchDuration = 2.5f;

        private readonly BotOwner _bot;

        private readonly BotComponentCache _cache;

        private readonly MovementContext _movement;

        private readonly BotPersonalityProfile _personality;

        private float _currentPoseLevel;

        private bool _isPoseLocked;

        private float _nextPoseCheck;

        private float _suppressedUntil;

        private float _targetPoseLevel;

        public BotPoseController(BotOwner bot, BotComponentCache cache)
        {
            this._bot = bot ?? throw new ArgumentNullException(nameof(bot));
            this._cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this._movement = bot.GetPlayer?.MovementContext
                             ?? throw new InvalidOperationException("Missing MovementContext.");
            this._personality = cache.AIRefactoredBotOwner?.PersonalityProfile
                                ?? throw new InvalidOperationException("Missing PersonalityProfile.");

            this._currentPoseLevel = this._movement.PoseLevel;
            this._targetPoseLevel = this._currentPoseLevel;
            this._nextPoseCheck = Time.time;
        }

        public float GetPoseLevel()
        {
            return this._movement.PoseLevel;
        }

        public void LockCrouchPose()
        {
            this._targetPoseLevel = 50f;
            this._isPoseLocked = true;
        }

        public void SetCrouch(bool anticipate = false)
        {
            this._targetPoseLevel = anticipate ? 60f : 50f;
        }

        public void SetProne(bool anticipate = false)
        {
            this._targetPoseLevel = anticipate ? 20f : 0f;
        }

        public void SetStand()
        {
            this._targetPoseLevel = 100f;
        }

        public void Tick(float currentTime)
        {
            if (this._bot.IsDead)
                return;

            if (this._isPoseLocked)
            {
                this.BlendPose(Time.deltaTime);
                return;
            }

            if (currentTime >= this._nextPoseCheck)
            {
                this._nextPoseCheck = currentTime + PoseCheckInterval;
                this.EvaluatePoseIntent(currentTime);
            }

            this.BlendPose(Time.deltaTime);
        }

        /// <summary>
        ///     Sets pose level based on nearby cover (uses NavPointRegistry scan).
        /// </summary>
        public void TrySetStanceFromNearbyCover(Vector3 position)
        {
            var nearbyPoints = NavPointRegistry.QueryNearby(
                position,
                4f,
                (Vector3 point) =>
                    {
                        var distSq = (point - position).sqrMagnitude;
                        return distSq <= 16f
                               && (BotCoverHelper.IsProneCover(point) || BotCoverHelper.IsLowCover(point));
                    });

            foreach (var point in nearbyPoints)
            {
                if (BotCoverHelper.IsProneCover(point)) this.SetProne(true);
                else if (BotCoverHelper.IsLowCover(point)) this.SetCrouch(true);
                else this.SetStand();

                break;
            }
        }

        public void UnlockPose()
        {
            this._isPoseLocked = false;
        }

        private void BlendPose(float deltaTime)
        {
            if (Mathf.Abs(this._currentPoseLevel - this._targetPoseLevel) < MinPoseThreshold)
                return;

            var panicFactor = this._cache.PanicHandler?.IsPanicking == true ? 0.6f : 1f;
            var combatFactor = this._cache.Combat?.IsInCombatState() == true ? 1f : 0.4f;
            var blendSpeed = PoseBlendSpeedBase * panicFactor * combatFactor;

            this._currentPoseLevel = Mathf.MoveTowards(
                this._currentPoseLevel,
                this._targetPoseLevel,
                blendSpeed * deltaTime);
            this._movement.SetPoseLevel(this._currentPoseLevel);
        }

        private void EvaluatePoseIntent(float currentTime)
        {
            if (this._cache.PanicHandler?.IsPanicking == true)
            {
                this._targetPoseLevel = 0f;
                return;
            }

            if (this._cache.Suppression?.IsSuppressed() == true)
                this._suppressedUntil = currentTime + SuppressionCrouchDuration;

            if (currentTime < this._suppressedUntil)
            {
                this._targetPoseLevel = 50f;
                return;
            }

            if (this._personality.IsFrenzied || this._personality.IsFearful
                                             || this._personality.Personality == PersonalityType.Sniper)
                if (this._bot.TryGetFlankDirection() is Vector3 flankDir)
                {
                    var flankAngle = Vector3.Angle(this._bot.LookDirection, flankDir.normalized);
                    if (flankAngle > FlankAngleThreshold)
                    {
                        this._targetPoseLevel = 0f;
                        return;
                    }
                }

            var maybeCover = this._bot.Memory?.BotCurrentCoverInfo?.LastCover;
            if (maybeCover != null)
            {
                var coverPos = maybeCover.Position;
                var dist = Vector3.Distance(this._bot.Position, coverPos);

                if (dist < 2.5f)
                {
                    if (BotCoverHelper.IsProneCover(maybeCover))
                    {
                        this._targetPoseLevel = 0f;
                        return;
                    }

                    if (BotCoverHelper.IsLowCover(maybeCover))
                    {
                        this._targetPoseLevel = 50f;
                        return;
                    }
                }
            }

            var inCombat = this._cache.Combat?.IsInCombatState() == true;
            var preferCrouch = this._personality.Caution > 0.6f || this._personality.IsCamper;

            this._targetPoseLevel = inCombat && preferCrouch ? 50f : 100f;
        }
    }
}