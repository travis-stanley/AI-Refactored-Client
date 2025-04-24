#nullable enable

using AIRefactored.AI.Core;
using EFT;
using System;
using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Scans for corners and ledges to trigger tactical lean and movement pauses.
    /// Aggressive bots peek faster and lean sooner. Defensive bots avoid ledges and corners.
    /// </summary>
    public class BotCornerScanner
    {
        #region Constants

        private const float BaseWallCheckDistance = 1.5f;
        private const float WallCheckHeight = 1.5f;
        private const float WallAngleThreshold = 0.7f;
        private const float BasePauseDuration = 0.4f;
        private const float EdgeRaySpacing = 0.25f;
        private const float EdgeCheckDistance = 1.25f;
        private const float MinFallHeight = 2.2f;
        private const float PrepCrouchTime = 0.75f;

        private static readonly LayerMask CoverCheckMask =
            LayerMask.GetMask("HighPolyCollider", "Terrain", "LowPolyCollider", "DoorLowPolyCollider");

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotPersonalityProfile? _profile;

        private float _pauseUntil;
        private float _prepCrouchUntil;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _bot = cache.Bot ?? throw new InvalidOperationException("Bot is null in cache.");
            _profile = cache.AIRefactoredBotOwner?.PersonalityProfile ?? throw new InvalidOperationException("Missing personality profile.");
        }

        #endregion

        #region Public API

        public void Tick(float time)
        {
            if (!IsEligible(time))
                return;

            if (IsApproachingEdge())
            {
                _cache?.Tilt?.Stop();
                PauseMovement(time);
                return;
            }

            if (TryCornerPeekWithCrouch(time))
                return;

            ResetLean(time);
        }

        #endregion

        #region Detection Logic

        private bool TryCornerPeekWithCrouch(float time)
        {
            if (_bot == null || _profile == null)
                return false;

            Vector3 origin = _bot.Position + _bot.Transform.up * WallCheckHeight;
            Vector3 right = _bot.Transform.right;
            Vector3 left = -right;
            float scanDistance = BaseWallCheckDistance + (1f - _profile.Caution) * 0.5f;

            if (Physics.Raycast(origin, left, out var hitL, scanDistance, CoverCheckMask) &&
                Vector3.Dot(hitL.normal, left) < WallAngleThreshold)
            {
                if (AttemptCrouch(time))
                    return true;

                TriggerLean(BotTiltType.left, time);
                return true;
            }

            if (Physics.Raycast(origin, right, out var hitR, scanDistance, CoverCheckMask) &&
                Vector3.Dot(hitR.normal, right) < WallAngleThreshold)
            {
                if (AttemptCrouch(time))
                    return true;

                TriggerLean(BotTiltType.right, time);
                return true;
            }

            return false;
        }

        private bool AttemptCrouch(float time)
        {
            if (_cache?.PoseController is { } pose && pose.GetPoseLevel() > 30f)
            {
                pose.SetCrouch();
                _prepCrouchUntil = time + PrepCrouchTime;
                return true;
            }

            return false;
        }

        private bool IsApproachingEdge()
        {
            if (_bot == null)
                return false;

            Vector3 start = _bot.Position + _bot.Transform.up * 0.2f;
            Vector3 forward = _bot.Transform.forward;

            int rayCount = Mathf.CeilToInt((EdgeRaySpacing * 2f) / EdgeRaySpacing) + 1;

            for (int i = 0; i < rayCount; i++)
            {
                float offset = (i - (rayCount - 1) / 2f) * EdgeRaySpacing;
                Vector3 origin = start + _bot.Transform.right * offset + forward * EdgeCheckDistance;

                if (!Physics.Raycast(origin, Vector3.down, MinFallHeight, CoverCheckMask))
                {
                    // Optionally: fallback to NavMesh check to reduce false positives on stairs
                    if (!NavMesh.SamplePosition(origin + Vector3.down * MinFallHeight, out _, 1.0f, NavMesh.AllAreas))
                        return true;
                }
            }

            return false;
        }

        #endregion

        #region Helpers

        private void TriggerLean(BotTiltType side, float time)
        {
            _cache?.Tilt?.Set(side);
            PauseMovement(time);
        }

        private void PauseMovement(float time)
        {
            if (_bot == null || _profile == null)
                return;

            float duration = BasePauseDuration * (0.5f + _profile.Caution);
            _bot.Mover.MovementPause(duration);
            _pauseUntil = time + duration;
        }

        private void ResetLean(float time)
        {
            var tilt = _cache?.Tilt;
            if (tilt != null && tilt._coreTilt)
            {
                tilt.tiltOff = time - 1f;
                tilt.ManualUpdate();
            }
        }

        private bool IsEligible(float time)
        {
            return _bot is { IsDead: false, Mover: not null } &&
                   time >= _pauseUntil &&
                   time >= _prepCrouchUntil &&
                   _bot.Memory?.GoalEnemy == null;
        }

        #endregion
    }
}
