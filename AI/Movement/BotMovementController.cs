#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System;
using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Controls advanced bot movement logic including inertia, smooth look, combat strafe, lean, jump, and flank mechanics.
    /// Designed for natural, player-like behavior and fluid real-time responsiveness.
    /// </summary>
    public class BotMovementController
    {
        #region Constants

        private const float CornerScanInterval = 1.2f;
        private const float ScanDistance = 2.5f;
        private const float ScanRadius = 0.25f;

        private const float LookSmoothSpeed = 6f;
        private const float InertiaWeight = 8f;
        private const float MinMoveThreshold = 0.05f;
        private const float LeanCooldown = 1.5f;

        private const float StuckThreshold = 0.1f;
        private const float MaxStuckDuration = 1.5f;

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotMovementTrajectoryPlanner? _trajectory;
        private BotJumpController? _jump;

        private readonly ManualLogSource _logger = AIRefactoredController.Logger;

        private float _nextScanTime;
        private Vector3 _lastVelocity;

        private float _stuckTimer;
        private float _lastMoveCheckTime;
        private Vector3 _lastSampledTarget = Vector3.zero;

        private bool _isStrafingRight = true;
        private float _strafeTimer;
        private float _nextLeanAllowed;
        private bool _inLootingMode;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes all movement dependencies with core references.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _bot = cache.Bot ?? throw new ArgumentNullException(nameof(cache.Bot));
            _trajectory = new BotMovementTrajectoryPlanner(_bot, cache);
            _jump = new BotJumpController(_bot, cache);

            _nextScanTime = Time.time;
            _lastVelocity = Vector3.zero;
        }

        #endregion

        #region Main Tick

        /// <summary>
        /// Ticks all runtime logic for movement, leaning, inertia, and directional control.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_bot?.IsDead != false || _cache == null || _bot.GetPlayer == null || !_bot.GetPlayer.IsAI)
                return;

            if (_bot.GetPlayer.HealthController?.IsAlive != true || _cache.PanicHandler?.IsPanicking == true)
                return;

            _jump?.Tick(deltaTime);

            if (_cache.DoorOpener != null && !_cache.DoorOpener.Update())
            {
                _logger.LogDebug($"[Movement] {_bot.Profile.Info.Nickname} is blocked by door interaction.");
                return;
            }

            if (Time.time >= _nextScanTime)
            {
                ScanAhead();
                _nextScanTime = Time.time + CornerScanInterval;
            }

            if (_bot.Mover != null)
            {
                Vector3 lookAt = _bot.Mover.LastTargetPoint(1.0f);
                SmoothLookTo(lookAt, deltaTime);
            }

            ApplyInertia(deltaTime);

            if (!_inLootingMode && _bot.Memory?.GoalEnemy != null && _bot.WeaponManager?.IsReady == true)
            {
                CombatStrafe(deltaTime);
                TryCombatLean();
                TryFlankAroundEnemy();
            }

            DetectStuck(deltaTime);
        }

        #endregion

        #region Looting Control

        public void EnterLootingMode() => _inLootingMode = true;
        public void ExitLootingMode() => _inLootingMode = false;

        #endregion

        #region Look Logic

        /// <summary>
        /// Smoothly rotates bot to face a target position over time.
        /// </summary>
        private void SmoothLookTo(Vector3 target, float deltaTime)
        {
            Vector3 dir = target - _bot!.Transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                return;

            if (_cache?.Tilt?._coreTilt == true && Vector3.Angle(_bot.Transform.forward, dir) > 80f)
                return;

            Quaternion desired = Quaternion.LookRotation(dir);
            _bot.Transform.rotation = Quaternion.Lerp(_bot.Transform.rotation, desired, LookSmoothSpeed * deltaTime);
        }

        #endregion

        #region Inertia

        /// <summary>
        /// Applies smooth movement transitions to simulate momentum and weighted response.
        /// </summary>
        private void ApplyInertia(float deltaTime)
        {
            if (_bot?.Mover == null || _bot.GetPlayer == null)
                return;

            Vector3 direction = _bot.Mover.LastTargetPoint(1.0f) - _bot.Position;
            direction.y = 0f;

            if (direction.magnitude < MinMoveThreshold)
                return;

            const float speed = 1.65f;
            Vector3 adjusted = _trajectory!.ModifyTrajectory(direction, deltaTime);
            Vector3 velocity = adjusted.normalized * speed;
            _lastVelocity = Vector3.Lerp(_lastVelocity, velocity, InertiaWeight * deltaTime);

            _bot.GetPlayer.CharacterController?.Move(_lastVelocity * deltaTime, deltaTime);
        }

        #endregion

        #region Strafing

        /// <summary>
        /// Executes combat-style strafe and avoidance near allies.
        /// </summary>
        private void CombatStrafe(float deltaTime)
        {
            if (_bot?.Mover == null || _bot.GetPlayer == null)
                return;

            _strafeTimer -= deltaTime;
            if (_strafeTimer <= 0f)
            {
                _isStrafingRight = UnityEngine.Random.value > 0.5f;
                _strafeTimer = UnityEngine.Random.Range(0.4f, 0.7f);
            }

            Vector3 baseStrafe = _isStrafingRight ? _bot.Transform.right : -_bot.Transform.right;
            Vector3 avoid = Vector3.zero;

            var group = _bot.BotsGroup;
            if (group != null)
            {
                for (int i = 0; i < group.MembersCount; i++)
                {
                    var mate = group.Member(i);
                    if (mate != null && mate != _bot && !mate.IsDead)
                    {
                        float dist = Vector3.Distance(_bot.Position, mate.Position);
                        if (dist < 2f && dist > 0.01f)
                            avoid += (_bot.Position - mate.Position).normalized / dist;
                    }
                }
            }

            Vector3 dir = (baseStrafe + avoid * 1.2f).normalized;
            float strafeSpeed = 1.2f + UnityEngine.Random.Range(-0.1f, 0.15f);

            _bot.GetPlayer.CharacterController?.Move(dir * strafeSpeed * deltaTime, deltaTime);
        }

        #endregion

        #region Leaning

        /// <summary>
        /// Determines if a lean is safe or tactically appropriate and executes it.
        /// </summary>
        private void TryCombatLean()
        {
            if (Time.time < _nextLeanAllowed || _cache?.Tilt == null)
                return;

            var profile = _cache.AIRefactoredBotOwner?.PersonalityProfile;
            if (profile == null || profile.LeaningStyle == LeanPreference.Never || _bot?.Memory?.GoalEnemy == null)
                return;

            Vector3 origin = _bot.Position + Vector3.up * 1.5f;
            bool wallLeft = Physics.Raycast(origin, -_bot.Transform.right, 1.5f);
            bool wallRight = Physics.Raycast(origin, _bot.Transform.right, 1.5f);

            Vector3? coverPos = _bot.Memory.BotCurrentCoverInfo?.LastCover?.Position;

            if (profile.LeaningStyle == LeanPreference.Conservative && !coverPos.HasValue && !wallLeft && !wallRight)
                return;

            if (coverPos.HasValue && !BotCoverHelper.WasRecentlyUsed(coverPos.Value))
            {
                BotCoverHelper.MarkUsed(coverPos.Value);
                float side = Vector3.Dot((_bot.Position - coverPos.Value).normalized, _bot.Transform.right);
                _cache.Tilt.Set(side > 0f ? BotTiltType.right : BotTiltType.left);
            }
            else if (wallLeft && !wallRight)
            {
                _cache.Tilt.Set(BotTiltType.right);
            }
            else if (wallRight && !wallLeft)
            {
                _cache.Tilt.Set(BotTiltType.left);
            }
            else
            {
                Vector3 toEnemy = _bot.Memory.GoalEnemy.CurrPosition - _bot.Position;
                float dot = Vector3.Dot(toEnemy.normalized, _bot.Transform.right);
                _cache.Tilt.Set(dot > 0f ? BotTiltType.right : BotTiltType.left);
            }

            _nextLeanAllowed = Time.time + LeanCooldown;
        }

        #endregion

        #region Flanking

        private void TryFlankAroundEnemy()
        {
            if (_bot?.Memory?.GoalEnemy == null)
                return;

            Vector3 pos = _bot.Position;
            Vector3 enemy = _bot.Memory.GoalEnemy.CurrPosition;
            float dist = Vector3.Distance(pos, enemy);

            if (dist < 22f && FlankPositionPlanner.TryFindFlankPosition(pos, enemy, out var flank))
            {
                BotMovementHelper.SmoothMoveTo(_bot, flank, false, 1f);
                _logger.LogDebug($"[Movement] {_bot.Profile.Info.Nickname} flanking to {flank}");
            }
        }

        #endregion

        #region Obstacle Scanning

        private void ScanAhead()
        {
            if (_bot?.Mover == null)
                return;

            Vector3 origin = _bot.Position + Vector3.up * 1.5f;
            Vector3 forward = _bot.LookDirection;

            if (Physics.SphereCast(origin, ScanRadius, forward, out RaycastHit _, ScanDistance))
            {
                if (UnityEngine.Random.value < 0.2f)
                    _bot.BotTalk?.TrySay(EPhraseTrigger.Look);
            }
        }

        #endregion

        #region Stuck Detection

        /// <summary>
        /// Detects if the bot is stuck (no motion for several frames) and triggers soft repath.
        /// </summary>
        private void DetectStuck(float deltaTime)
        {
            if (_bot?.Mover == null || _bot.GetPlayer == null || _inLootingMode)
                return;

            Vector3 target = _bot.Mover.LastTargetPoint(1.0f);
            if (!ValidateNavMeshTarget(target))
                return;

            Vector3 movement = _bot.GetPlayer.Velocity;
            float speedSq = movement.sqrMagnitude;

            if (speedSq < StuckThreshold * StuckThreshold)
            {
                _stuckTimer += deltaTime;
                if (_stuckTimer > MaxStuckDuration)
                {
                    BotMovementHelper.SmoothMoveTo(_bot, target, false, 1f);
                    _logger.LogDebug($"[Movement] {_bot.Profile.Info.Nickname} triggered soft repath due to low velocity.");
                    _stuckTimer = 0f;
                }
            }
            else
            {
                _stuckTimer = 0f;
            }
        }

        /// <summary>
        /// Validates if a move destination is located on the NavMesh.
        /// </summary>
        private bool ValidateNavMeshTarget(Vector3 position)
        {
            if (NavMesh.SamplePosition(position, out var hit, 1.5f, NavMesh.AllAreas))
            {
                if ((hit.position - position).sqrMagnitude < 1f)
                    return true;
            }

            _logger.LogWarning($"[Movement] Invalid NavMesh target: {position}");
            return false;
        }

        #endregion
    }
}
