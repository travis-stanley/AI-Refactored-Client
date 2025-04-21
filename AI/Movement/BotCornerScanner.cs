#nullable enable

using AIRefactored.AI.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Detects corners, edges, and blind angles using raycasts.
    /// Triggers lean or movement pauses for tactical entry into tight areas.
    /// </summary>
    public class BotCornerScanner
    {
        #region Constants

        private const float WallCheckDistance = 1.5f;
        private const float WallCheckHeight = 1.4f;
        private const float PauseDuration = 0.4f;
        private const float WallAngleThreshold = 0.7f;
        private const float EdgeCheckDistance = 1.25f;
        private const float EdgeRaySpacing = 0.25f;
        private const float MinFallHeight = 2.2f;

        #endregion

        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly BotPersonalityProfile _personality;
        private float _pauseUntil = 0f;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a corner scanner for this bot.
        /// </summary>
        public BotCornerScanner(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _personality = cache.AIRefactoredBotOwner?.PersonalityProfile ?? new BotPersonalityProfile();
        }

        #endregion

        #region Tick Logic

        /// <summary>
        /// Main logic to evaluate corner or edge checks.
        /// </summary>
        public void Tick(float time)
        {
            if (_bot.IsDead || _bot.Mover == null || _bot.Memory?.GoalEnemy != null)
                return;

            if (time < _pauseUntil)
                return;

            if (_personality.Caution < 0.35f && !_personality.IsSilentHunter && !_personality.IsCamper)
                return;

            if (IsApproachingEdge())
            {
                _cache.Tilt?.Stop();
                _bot.Mover.MovementPause(PauseDuration);
                _pauseUntil = time + PauseDuration;
                return;
            }

            if (TryScanForCornerPeek(time))
                return;

            ResetLean(time);
        }

        #endregion

        #region Core Scanning

        /// <summary>
        /// Scans left and right for tight wall angles to peek.
        /// </summary>
        private bool TryScanForCornerPeek(float time)
        {
            Vector3 origin = _bot.Position + Vector3.up * WallCheckHeight;
            Vector3 left = -_bot.Transform.right;
            Vector3 right = _bot.Transform.right;

            if (Physics.Raycast(origin, left, out RaycastHit hitLeft, WallCheckDistance))
            {
                if (IsAngledWall(hitLeft.normal, left))
                {
                    TriggerLean(BotTiltType.left, time);
                    return true;
                }
            }

            if (Physics.Raycast(origin, right, out RaycastHit hitRight, WallCheckDistance))
            {
                if (IsAngledWall(hitRight.normal, right))
                {
                    TriggerLean(BotTiltType.right, time);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks directly ahead to see if bot is near a ledge.
        /// </summary>
        private bool IsApproachingEdge()
        {
            Vector3 start = _bot.Position + Vector3.up * 0.2f;
            Vector3 forward = _bot.Transform.forward;

            for (float offset = -EdgeRaySpacing; offset <= EdgeRaySpacing; offset += EdgeRaySpacing)
            {
                Vector3 offsetDir = _bot.Transform.right * offset;
                Vector3 origin = start + offsetDir + forward * EdgeCheckDistance;
                Vector3 down = Vector3.down;

                if (!Physics.Raycast(origin, down, MinFallHeight))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Helpers

        private void TriggerLean(BotTiltType side, float time)
        {
            _cache.Tilt?.Set(side);
            _pauseUntil = time + PauseDuration;
        }

        private void ResetLean(float time)
        {
            if (_cache.Tilt != null && _cache.Tilt._coreTilt)
            {
                _cache.Tilt.tiltOff = time - 1f;
                _cache.Tilt.ManualUpdate();
            }
        }

        private static bool IsAngledWall(Vector3 wallNormal, Vector3 scanDir)
        {
            return Vector3.Dot(wallNormal, scanDir.normalized) < WallAngleThreshold;
        }

        #endregion
    }
}
