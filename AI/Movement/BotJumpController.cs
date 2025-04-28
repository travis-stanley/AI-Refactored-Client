#nullable enable

namespace AIRefactored.AI.Movement
{
    using System;

    using AIRefactored.AI.Core;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Handles dynamic jumping behavior for bots. Detects jumpable obstacles, triggers safe vaults,
    ///     and enforces physical constraints to prevent unrealistic jumps. Fully realistic and headless-safe.
    /// </summary>
    public sealed class BotJumpController
    {
        private const float JumpCheckDistance = 1.1f;

        private const float JumpCooldown = 1.25f;

        private const float MaxJumpHeight = 1.2f;

        private const float MinJumpHeight = 0.3f;

        private const float ObstacleCheckRadius = 0.4f;

        private const float SafeFallHeight = 2.2f;

        private const float VaultForwardOffset = 0.75f;

        private readonly BotOwner _bot;

        private readonly BotComponentCache _cache;

        private readonly MovementContext _context;

        private bool _hasRecentlyJumped;

        private float _lastJumpTime;

        public BotJumpController(BotOwner bot, BotComponentCache cache)
        {
            this._bot = bot ?? throw new ArgumentNullException(nameof(bot));
            this._cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this._context = this._bot.GetPlayer?.MovementContext
                            ?? throw new InvalidOperationException("Missing MovementContext.");
        }

        /// <summary>
        ///     Evaluates whether a jump is needed, and triggers it if safe and appropriate.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!this.IsJumpAllowed())
                return;

            if (this.TryFindJumpTarget(out var target)) this.ExecuteJump(target);
        }

        private void ExecuteJump(Vector3 landingPosition)
        {
            this._context.OnJump();
            this._lastJumpTime = Time.time;
            this._hasRecentlyJumped = true;

            var delta = landingPosition - this._context.TransformPosition;
            this._context.ApplyMotion(delta, 0.25f);
        }

        private bool IsJumpAllowed()
        {
            if (this._hasRecentlyJumped && Time.time - this._lastJumpTime < JumpCooldown)
                return false;

            if (!this._context.IsGrounded || this._context.IsInPronePose)
                return false;

            if (this._cache.PanicHandler?.IsPanicking == true)
                return false;

            return true;
        }

        private bool TryFindJumpTarget(out Vector3 target)
        {
            target = Vector3.zero;

            var origin = this._context.PlayerColliderCenter + Vector3.up * 0.25f;
            var direction = this._context.TransformForwardVector;

            if (!Physics.SphereCast(origin, ObstacleCheckRadius, direction, out var hit, JumpCheckDistance))
                return false;

            var col = hit.collider;
            if (col == null)
                return false;

            var obstacleTop = col.bounds.max;
            var heightDelta = obstacleTop.y - this._context.TransformPosition.y;

            if (heightDelta < MinJumpHeight || heightDelta > MaxJumpHeight)
                return false;

            var probePoint = obstacleTop + direction * VaultForwardOffset;

            if (!Physics.Raycast(probePoint, Vector3.down, out var landing, 2.5f))
                return false;

            var fallDelta = this._context.TransformPosition.y - landing.point.y;
            if (fallDelta > SafeFallHeight)
                return false;

            target = landing.point;
            return true;
        }
    }
}