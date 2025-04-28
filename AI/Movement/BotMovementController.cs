#nullable enable

namespace AIRefactored.AI.Movement
{
    using System;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using EFT;

    using UnityEngine;
    using UnityEngine.AI;

    using Random = UnityEngine.Random;

    /// <summary>
    ///     Controls advanced bot movement logic including inertia, smooth look, combat strafe, lean, jump, and flank
    ///     mechanics.
    ///     Designed for natural, player-like behavior and fluid real-time responsiveness.
    /// </summary>
    public class BotMovementController
    {
        private const float CornerScanInterval = 1.2f;

        private const float InertiaWeight = 8f;

        private const float LeanCooldown = 1.5f;

        private const float LookSmoothSpeed = 6f;

        private const float MaxStuckDuration = 1.5f;

        private const float MinMoveThreshold = 0.05f;

        private const float ScanDistance = 2.5f;

        private const float ScanRadius = 0.25f;

        private const float StuckThreshold = 0.1f;

        private readonly ManualLogSource _logger = AIRefactoredController.Logger;

        private BotOwner? _bot;

        private BotComponentCache? _cache;

        private bool _inLootingMode;

        private bool _isStrafingRight = true;

        private BotJumpController? _jump;

        private float _lastMoveCheckTime;

        private Vector3 _lastSampledTarget = Vector3.zero;

        private Vector3 _lastVelocity;

        private float _nextLeanAllowed;

        private float _nextScanTime;

        private float _strafeTimer;

        private float _stuckTimer;

        private BotMovementTrajectoryPlanner? _trajectory;

        public void EnterLootingMode()
        {
            this._inLootingMode = true;
        }

        public void ExitLootingMode()
        {
            this._inLootingMode = false;
        }

        /// <summary>
        ///     Initializes all movement dependencies with core references.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this._bot = cache.Bot ?? throw new ArgumentNullException(nameof(cache.Bot));
            this._trajectory = new BotMovementTrajectoryPlanner(this._bot, cache);
            this._jump = new BotJumpController(this._bot, cache);

            this._nextScanTime = Time.time;
            this._lastVelocity = Vector3.zero;
        }

        /// <summary>
        ///     Ticks all runtime logic for movement, leaning, inertia, and directional control.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (this._bot?.IsDead != false || this._cache == null || this._bot.GetPlayer == null
                || !this._bot.GetPlayer.IsAI)
                return;

            if (this._bot.GetPlayer.HealthController?.IsAlive != true || this._cache.PanicHandler?.IsPanicking == true)
                return;

            this._jump?.Tick(deltaTime);

            if (this._cache.DoorOpener != null && !this._cache.DoorOpener.Update())
            {
                this._logger.LogDebug($"[Movement] {this._bot.Profile.Info.Nickname} is blocked by door interaction.");
                return;
            }

            if (Time.time >= this._nextScanTime)
            {
                this.ScanAhead();
                this._nextScanTime = Time.time + CornerScanInterval;
            }

            if (this._bot.Mover != null)
            {
                var lookAt = this._bot.Mover.LastTargetPoint(1.0f);
                this.SmoothLookTo(lookAt, deltaTime);
            }

            this.ApplyInertia(deltaTime);

            if (!this._inLootingMode && this._bot.Memory?.GoalEnemy != null && this._bot.WeaponManager?.IsReady == true)
            {
                this.CombatStrafe(deltaTime);
                this.TryCombatLean();
                this.TryFlankAroundEnemy();
            }

            this.DetectStuck(deltaTime);
        }

        /// <summary>
        ///     Applies smooth movement transitions to simulate momentum and weighted response.
        /// </summary>
        private void ApplyInertia(float deltaTime)
        {
            if (this._bot?.Mover == null || this._bot.GetPlayer == null)
                return;

            var direction = this._bot.Mover.LastTargetPoint(1.0f) - this._bot.Position;
            direction.y = 0f;

            if (direction.magnitude < MinMoveThreshold)
                return;

            const float speed = 1.65f;
            var adjusted = this._trajectory!.ModifyTrajectory(direction, deltaTime);
            var velocity = adjusted.normalized * speed;
            this._lastVelocity = Vector3.Lerp(this._lastVelocity, velocity, InertiaWeight * deltaTime);

            this._bot.GetPlayer.CharacterController?.Move(this._lastVelocity * deltaTime, deltaTime);
        }

        /// <summary>
        ///     Executes combat-style strafe and avoidance near allies.
        /// </summary>
        private void CombatStrafe(float deltaTime)
        {
            if (this._bot?.Mover == null || this._bot.GetPlayer == null)
                return;

            this._strafeTimer -= deltaTime;
            if (this._strafeTimer <= 0f)
            {
                this._isStrafingRight = Random.value > 0.5f;
                this._strafeTimer = Random.Range(0.4f, 0.7f);
            }

            var baseStrafe = this._isStrafingRight ? this._bot.Transform.right : -this._bot.Transform.right;
            var avoid = Vector3.zero;

            var group = this._bot.BotsGroup;
            if (group != null)
                for (var i = 0; i < group.MembersCount; i++)
                {
                    var mate = group.Member(i);
                    if (mate != null && mate != this._bot && !mate.IsDead)
                    {
                        var dist = Vector3.Distance(this._bot.Position, mate.Position);
                        if (dist < 2f && dist > 0.01f)
                            avoid += (this._bot.Position - mate.Position).normalized / dist;
                    }
                }

            var dir = (baseStrafe + avoid * 1.2f).normalized;
            var strafeSpeed = 1.2f + Random.Range(-0.1f, 0.15f);

            this._bot.GetPlayer.CharacterController?.Move(dir * strafeSpeed * deltaTime, deltaTime);
        }

        /// <summary>
        ///     Detects if the bot is stuck (no motion for several frames) and triggers soft repath.
        /// </summary>
        private void DetectStuck(float deltaTime)
        {
            if (this._bot?.Mover == null || this._bot.GetPlayer == null || this._inLootingMode)
                return;

            var target = this._bot.Mover.LastTargetPoint(1.0f);
            if (!this.ValidateNavMeshTarget(target))
                return;

            var movement = this._bot.GetPlayer.Velocity;
            var speedSq = movement.sqrMagnitude;

            if (speedSq < StuckThreshold * StuckThreshold)
            {
                this._stuckTimer += deltaTime;
                if (this._stuckTimer > MaxStuckDuration)
                {
                    BotMovementHelper.SmoothMoveTo(this._bot, target, false);
                    this._logger.LogDebug(
                        $"[Movement] {this._bot.Profile.Info.Nickname} triggered soft repath due to low velocity.");
                    this._stuckTimer = 0f;
                }
            }
            else
            {
                this._stuckTimer = 0f;
            }
        }

        private void ScanAhead()
        {
            if (this._bot?.Mover == null)
                return;

            var origin = this._bot.Position + Vector3.up * 1.5f;
            var forward = this._bot.LookDirection;

            if (Physics.SphereCast(origin, ScanRadius, forward, out var _, ScanDistance))
                if (Random.value < 0.2f)
                    this._bot.BotTalk?.TrySay(EPhraseTrigger.Look);
        }

        /// <summary>
        ///     Smoothly rotates bot to face a target position over time.
        /// </summary>
        private void SmoothLookTo(Vector3 target, float deltaTime)
        {
            var dir = target - this._bot!.Transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                return;

            if (this._cache?.Tilt?._coreTilt == true && Vector3.Angle(this._bot.Transform.forward, dir) > 80f)
                return;

            var desired = Quaternion.LookRotation(dir);
            this._bot.Transform.rotation = Quaternion.Lerp(
                this._bot.Transform.rotation,
                desired,
                LookSmoothSpeed * deltaTime);
        }

        /// <summary>
        ///     Determines if a lean is safe or tactically appropriate and executes it.
        /// </summary>
        private void TryCombatLean()
        {
            if (Time.time < this._nextLeanAllowed || this._cache?.Tilt == null)
                return;

            var profile = this._cache.AIRefactoredBotOwner?.PersonalityProfile;
            if (profile == null || profile.LeaningStyle == LeanPreference.Never || this._bot?.Memory?.GoalEnemy == null)
                return;

            var origin = this._bot.Position + Vector3.up * 1.5f;
            var wallLeft = Physics.Raycast(origin, -this._bot.Transform.right, 1.5f);
            var wallRight = Physics.Raycast(origin, this._bot.Transform.right, 1.5f);

            var coverPos = this._bot.Memory.BotCurrentCoverInfo?.LastCover?.Position;

            if (profile.LeaningStyle == LeanPreference.Conservative && !coverPos.HasValue && !wallLeft && !wallRight)
                return;

            if (coverPos.HasValue && !BotCoverHelper.WasRecentlyUsed(coverPos.Value))
            {
                BotCoverHelper.MarkUsed(coverPos.Value);
                var side = Vector3.Dot((this._bot.Position - coverPos.Value).normalized, this._bot.Transform.right);
                this._cache.Tilt.Set(side > 0f ? BotTiltType.right : BotTiltType.left);
            }
            else if (wallLeft && !wallRight)
            {
                this._cache.Tilt.Set(BotTiltType.right);
            }
            else if (wallRight && !wallLeft)
            {
                this._cache.Tilt.Set(BotTiltType.left);
            }
            else
            {
                var toEnemy = this._bot.Memory.GoalEnemy.CurrPosition - this._bot.Position;
                var dot = Vector3.Dot(toEnemy.normalized, this._bot.Transform.right);
                this._cache.Tilt.Set(dot > 0f ? BotTiltType.right : BotTiltType.left);
            }

            this._nextLeanAllowed = Time.time + LeanCooldown;
        }

        private void TryFlankAroundEnemy()
        {
            if (this._bot?.Memory?.GoalEnemy == null)
                return;

            var pos = this._bot.Position;
            var enemy = this._bot.Memory.GoalEnemy.CurrPosition;
            var dist = Vector3.Distance(pos, enemy);

            if (dist < 22f && FlankPositionPlanner.TryFindFlankPosition(pos, enemy, out var flank))
            {
                BotMovementHelper.SmoothMoveTo(this._bot, flank, false);
                this._logger.LogDebug($"[Movement] {this._bot.Profile.Info.Nickname} flanking to {flank}");
            }
        }

        /// <summary>
        ///     Validates if a move destination is located on the NavMesh.
        /// </summary>
        private bool ValidateNavMeshTarget(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out var hit, 1.5f, NavMesh.AllAreas))
                if ((hit.position - position).sqrMagnitude < 1f)
                    return true;

            this._logger.LogWarning($"[Movement] Invalid NavMesh target: {position}");
            return false;
        }
    }
}