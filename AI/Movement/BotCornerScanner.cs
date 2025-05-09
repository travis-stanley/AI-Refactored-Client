﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

#nullable enable

namespace AIRefactored.AI.Movement
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Scans for edges and corners to trigger realistic lean, crouch, or pause behavior.
    /// Aggressive bots peek corners quickly, cautious bots crouch before engaging.
    /// </summary>
    public sealed class BotCornerScanner
    {
        #region Constants

        private const float BasePauseDuration = 0.4f;
        private const float BaseWallCheckDistance = 1.5f;
        private const float EdgeCheckDistance = 1.25f;
        private const float EdgeRaySpacing = 0.25f;
        private const float MinFallHeight = 2.2f;
        private const float PrepCrouchTime = 0.75f;
        private const float WallAngleThreshold = 0.7f;
        private const float WallCheckHeight = 1.5f;
        private const float NavSampleTolerance = 0.65f;

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotPersonalityProfile? _profile;
        private float _pauseUntil;
        private float _prepCrouchUntil;
        private bool _isLeaning;
        private bool _isCrouching;

        #endregion

        #region Constructors

        /// <summary>
        /// Constructs and initializes the corner scanner using provided bot and cache.
        /// </summary>
        /// <param name="bot">The EFT BotOwner instance.</param>
        /// <param name="cache">BotComponentCache instance.</param>
        public BotCornerScanner(BotOwner bot, BotComponentCache cache)
        {
            if (bot == null) throw new ArgumentNullException(nameof(bot));
            if (cache == null) throw new ArgumentNullException(nameof(cache));
            if (cache.AIRefactoredBotOwner?.PersonalityProfile == null)
                throw new ArgumentException("Cache is missing AIRefactoredBotOwner or profile.");

            _bot = bot;
            _cache = cache;
            _profile = cache.AIRefactoredBotOwner.PersonalityProfile;
        }

        /// <summary>
        /// Default constructor for manual Init fallback.
        /// </summary>
        public BotCornerScanner()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the corner scanner using bot context.
        /// </summary>
        /// <param name="cache">Bot component cache.</param>
        public void Initialize(BotComponentCache cache)
        {
            if (cache == null || cache.Bot == null || cache.AIRefactoredBotOwner?.PersonalityProfile == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            _cache = cache;
            _bot = cache.Bot;
            _profile = cache.AIRefactoredBotOwner.PersonalityProfile;
        }

        /// <summary>
        /// Scans edges and walls to trigger pause, crouch, or lean based on tactical need.
        /// </summary>
        /// <param name="time">World time for timing checks.</param>
        public void Tick(float time)
        {
            if (!IsEligible(time))
            {
                return;
            }

            if (IsApproachingEdge())
            {
                _cache?.Tilt?.Stop();
                PauseMovement(time);
                return;
            }

            if (TryCornerPeekWithCrouch(time))
            {
                return;
            }

            ResetLean(time);
        }

        #endregion

        #region Internal Logic

        private bool IsEligible(float time)
        {
            return _bot != null &&
                   !_bot.IsDead &&
                   _bot.Mover != null &&
                   _bot.Transform != null &&
                   _bot.Memory?.GoalEnemy == null &&
                   time >= _pauseUntil &&
                   time >= _prepCrouchUntil;
        }

        private void PauseMovement(float time)
        {
            if (_bot?.Mover == null || _profile == null)
            {
                return;
            }

            float duration = BasePauseDuration * Mathf.Clamp(0.5f + _profile.Caution, 0.35f, 2.0f);
            _bot.Mover.MovementPause(duration);
            _pauseUntil = time + duration;
        }

        private bool IsApproachingEdge()
        {
            if (_bot?.Transform == null)
            {
                return false;
            }

            Vector3 origin = _bot.Position + Vector3.up * 0.2f;
            Vector3 forward = _bot.Transform.forward;
            Vector3 right = _bot.Transform.right;
            int rays = Mathf.CeilToInt((EdgeCheckDistance * 2f) / EdgeRaySpacing);

            for (int i = 0; i < rays; i++)
            {
                float offset = (i - (rays / 2f)) * EdgeRaySpacing;
                Vector3 rayOrigin = origin + (right * offset) + (forward * EdgeCheckDistance);

                if (!Physics.Raycast(rayOrigin, Vector3.down, MinFallHeight, AIRefactoredLayerMasks.NavObstacleMask))
                {
                    if (!NavMesh.SamplePosition(rayOrigin + Vector3.down * MinFallHeight, out NavMeshHit hit, 1.0f, NavMesh.AllAreas) ||
                        hit.distance > NavSampleTolerance)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryCornerPeekWithCrouch(float time)
        {
            if (_bot?.Transform == null || _profile == null)
            {
                return false;
            }

            Vector3 origin = _bot.Position + Vector3.up * WallCheckHeight;
            Vector3 right = _bot.Transform.right;
            Vector3 left = -right;
            float checkDist = BaseWallCheckDistance + ((1f - _profile.Caution) * 0.5f);

            if (CheckWall(origin, left, checkDist))
            {
                return TriggerLeanOrCrouch(BotTiltType.left, time);
            }

            if (CheckWall(origin, right, checkDist))
            {
                return TriggerLeanOrCrouch(BotTiltType.right, time);
            }

            return false;
        }

        private bool CheckWall(Vector3 origin, Vector3 direction, float distance)
        {
            if (Physics.Raycast(origin, direction, out RaycastHit hit, distance, AIRefactoredLayerMasks.CoverRayMask))
            {
                return Vector3.Dot(hit.normal, direction) < WallAngleThreshold;
            }

            return false;
        }

        private bool TriggerLeanOrCrouch(BotTiltType side, float time)
        {
            if (!_isCrouching && AttemptCrouch(time))
            {
                _isCrouching = true;
                return true;
            }

            if (!_isLeaning)
            {
                _cache?.Tilt?.Set(side);
                PauseMovement(time);
                _isLeaning = true;
            }

            return true;
        }

        private bool AttemptCrouch(float time)
        {
            if (_cache?.PoseController == null)
            {
                return false;
            }

            if (_cache.PoseController.GetPoseLevel() > 30f)
            {
                _cache.PoseController.SetCrouch();
                _prepCrouchUntil = time + PrepCrouchTime;
                return true;
            }

            return false;
        }

        private void ResetLean(float time)
        {
            if (_cache?.Tilt?._coreTilt == true)
            {
                _cache.Tilt.tiltOff = time - 1f;
                _cache.Tilt.ManualUpdate();
                _isLeaning = false;
            }
        }

        #endregion
    }
}
