#nullable enable

using AIRefactored.AI.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Handles dynamic jumping behavior for bots. Detects jumpable obstacles, triggers safe vaults,
    /// and enforces physical constraints to prevent unrealistic jumps. Fully realistic and headless-safe.
    /// </summary>
    public sealed class BotJumpController
    {
        #region Configuration

        private const float MaxJumpHeight = 1.2f;
        private const float MinJumpHeight = 0.3f;
        private const float JumpCheckDistance = 1.1f;
        private const float SafeFallHeight = 2.2f;
        private const float ObstacleCheckRadius = 0.4f;
        private const float JumpCooldown = 1.25f;
        private const float VaultForwardOffset = 0.75f;

        #endregion

        #region Fields

        private readonly BotOwner _bot;
        private readonly MovementContext _context;
        private readonly BotComponentCache _cache;

        private float _lastJumpTime;
        private bool _hasRecentlyJumped;

        #endregion

        #region Constructor

        public BotJumpController(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _context = bot.GetPlayer.MovementContext;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Evaluates whether a jump is needed, and triggers it if safe and appropriate.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!IsJumpAllowed())
                return;

            if (TryFindJumpTarget(out Vector3 jumpTarget))
                ExecuteJump(jumpTarget);
        }

        #endregion

        #region Jump Eligibility Logic

        private bool IsJumpAllowed()
        {
            if (_hasRecentlyJumped && Time.time - _lastJumpTime < JumpCooldown)
                return false;

            if (_context.IsInPronePose || !_context.IsGrounded)
                return false;

            if (_cache.PanicHandler?.IsPanicking == true)
                return false;

            return true;
        }

        #endregion

        #region Obstacle Scanning

        private bool TryFindJumpTarget(out Vector3 target)
        {
            target = Vector3.zero;

            Vector3 origin = _context.PlayerColliderCenter + Vector3.up * 0.25f;
            Vector3 forward = _context.TransformForwardVector;

            if (!Physics.SphereCast(origin, ObstacleCheckRadius, forward, out RaycastHit hit, JumpCheckDistance))
                return false;

            Collider obstacle = hit.collider;
            Vector3 obstacleTop = obstacle.bounds.max;
            float relativeHeight = obstacleTop.y - _context.TransformPosition.y;

            if (relativeHeight < MinJumpHeight || relativeHeight > MaxJumpHeight)
                return false;

            Vector3 landingProbe = obstacleTop + forward * VaultForwardOffset;

            if (!Physics.Raycast(landingProbe, Vector3.down, out RaycastHit landing, 2.5f))
                return false;

            float fallDelta = _context.TransformPosition.y - landing.point.y;
            if (fallDelta > SafeFallHeight)
                return false;

            target = landing.point;
            return true;
        }

        #endregion

        #region Jump Execution

        private void ExecuteJump(Vector3 landingPosition)
        {
            _context.OnJump();
            _lastJumpTime = Time.time;
            _hasRecentlyJumped = true;

            Vector3 impulse = landingPosition - _context.TransformPosition;
            _context.ApplyMotion(impulse, 0.25f);
        }

        #endregion
    }
}
