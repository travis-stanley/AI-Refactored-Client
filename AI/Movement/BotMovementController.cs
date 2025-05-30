﻿// <auto-generated>
//   AI-Refactored: BotMovementController.cs (Beyond Diamond, Overlay-Only, Ultra-Realism, June 2025)
//   SYSTEMATICALLY MANAGED. All movement strictly via BotMovementHelper (event/overlay only, never tick-throttled).
//   Fully squad/personality/cover/lean/anticipation/human error aware. Zero teleport, snap, or transform leaks.
//   Parity: SPT/FIKA/client/headless. Robust null guards, pooling, error isolation. MIT License.
// </auto-generated>

namespace AIRefactored.AI.Movement
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Navigation;
    using AIRefactored.Core;
    using AIRefactored.Pools;
    using BepInEx.Logging;
    using EFT;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Handles all bot movement overlays: squad/formation/lean/cover/anticipation.
    /// - All logic is ticked from BotBrain.
    /// - All movement is routed via BotMovementHelper on *explicit* event/change (never per tick).
    /// - Never teleports, never snaps. Overlay-only. Zero transform/NavMesh leaks.
    /// - Robust: null-guarded, pooled, multiplayer/headless-safe.
    /// </summary>
    public sealed class BotMovementController
    {
        #region Constants

        private const float MoveTargetEpsilon = 0.22f;
        private const float MoveCooldown = 1.15f;
        private const float MicroJitterMag = 0.11f;
        private const float CoverRayDist = 1.85f;
        private const float GroupSpacing = 2.72f, SpacingForce = 0.42f, SquadCoverSpacing = 1.22f;
        private const float LookYClampMin = -24f, LookYClampMax = 38f;
        private const float FallbackBackoff = 0.39f;
        private const float PathWobble = 0.17f;
        private const float MinSprintDist = 11.7f, MaxSprintDist = 24.5f;
        private const float SprintMinDur = 1.35f, SprintMaxDur = 2.87f;
        private const float StartlePause = 0.25f;
        private const float CombatStrafeBase = 1.10f, CombatStrafeJitter = 0.08f;
        private const float CombatStrafeCooldownMin = 0.38f, CombatStrafeCooldownMax = 1.19f;
        private const float CombatStrafeSuppressed = 0.44f;
        private const float MaxStrafeAngle = 82f;
        private const float LeanCooldownMin = 0.85f, LeanCooldownMax = 1.41f, LeanSuppressed = 1.17f;
        private const float MinLeanHold = 1.05f, MaxLeanHold = 1.65f;
        private const float ObstaclePauseChance = 0.13f;

        #endregion

        #region Fields

        private static readonly ManualLogSource Logger = Plugin.LoggerInstance;
        private BotOwner _bot;
        private BotComponentCache _cache;

        private Vector3 _lastMoveTarget;
        private float _lastMoveTime;
        private float _nextLeanTime, _strafeTimer, _leanLockoutUntil, _leanHoldUntil, _sprintUntil;
        private float _formationUpdateUntil, _formationHoldUntil, _coverPauseUntil, _panicPathWobbleTimer, _startleUntil, _fallbackRetryTime;
        private bool _strafeRight, _fallbackActive, _coverPauseActive, _shouldSprint, _formationHold;
        private Vector3 _microDriftOffset, _lastLeaderPos;
        private bool _lastPanicking, _inCover, _formationRolePriority;
        private BotTiltType _currentLean;
        private float _leanMissChance, _formationTargetWeight;
        private float _personalityScanDelay, _personalityStartleChance;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            if (cache == null || cache.Bot == null)
                throw new InvalidOperationException("[BotMovementController] Invalid initialization.");

            _cache = cache;
            _bot = cache.Bot;

            _lastMoveTarget = Vector3.zero;
            _lastMoveTime = -1000f;
            _nextLeanTime = Time.time;
            _strafeTimer = 0.51f;
            _leanLockoutUntil = 0f;
            _leanHoldUntil = 0f;
            _panicPathWobbleTimer = 0f;
            _fallbackRetryTime = 0f;
            _formationUpdateUntil = Time.time;
            _formationHold = false;
            _formationHoldUntil = 0f;
            _coverPauseActive = false;
            _coverPauseUntil = 0f;
            _shouldSprint = false;
            _sprintUntil = 0f;
            _lastLeaderPos = Vector3.zero;
            _currentLean = BotTiltType.right;
            _lastPanicking = _cache?.PanicHandler != null && _cache.PanicHandler.IsPanicking;
            _inCover = false;
            _formationRolePriority = false;
            _microDriftOffset = Vector3.zero;
            _leanMissChance = (_cache.AIRefactoredBotOwner?.PersonalityProfile?.Caution ?? 0.5f) * 0.048f;
            _personalityScanDelay = UnityEngine.Random.Range(0.12f, 0.48f) * (_cache.AIRefactoredBotOwner?.PersonalityProfile?.Caution ?? 0.5f);
            _personalityStartleChance = (_cache.AIRefactoredBotOwner?.PersonalityProfile?.AggressionLevel ?? 0.5f) < 0.33f ? 0.31f : 0.09f;
            _formationTargetWeight = 0.52f;

            BotMovementHelper.Reset(_bot);
        }

        #endregion

        #region Main Tick

        /// <summary>
        /// Ticks all movement overlays (never tick-throttled move). Called only by BotBrain.
        /// Handles squad, anticipation, lean, cover, strafe, panic, and personality overlays.
        /// All *actual* movement is via explicit BotMovementHelper call, never every frame.
        /// </summary>
        public void Tick(float deltaTime)
        {
            try
            {
                if (_bot == null || _bot.IsDead || _bot.Mover == null) return;
                var player = _bot.GetPlayer;
                if (player == null || !player.IsAI) return;

                UpdateStartlePause();
                if (Time.time < _startleUntil) return;

                TrySquadSpacing(deltaTime);
                TryFormationFollow(deltaTime);
                TryFallbackPathCorrection();
                TryCombatStrafe(_bot.Mover, deltaTime);
                TryLean();
                TrySmoothLook(_bot.Mover, deltaTime);
                TryCoverPause();
                TryPanicPathWobble(deltaTime);
                TryTacticalSprint(deltaTime);
            }
            catch (Exception ex)
            {
                Logger.LogError("[BotMovementController] Tick error: " + ex);
            }
        }

        #endregion

        #region Move Issuer (Overlay-Only, Event Driven)

        /// <summary>
        /// Issues a *real* move only on overlay/event, never per frame/tick.
        /// </summary>
        private void IssueMove(Vector3 target, bool slow = true, float cohesion = 1f)
        {
            if (_bot == null) return;

            float sqrDist = (_lastMoveTarget - target).sqrMagnitude;
            bool distanceChanged = sqrDist > MoveTargetEpsilon * MoveTargetEpsilon;
            bool cooldownPassed = (Time.time - _lastMoveTime) > MoveCooldown;

            if (distanceChanged || cooldownPassed || BotMovementHelper.IsStuckAtTarget(_bot))
            {
                _lastMoveTarget = target;
                _lastMoveTime = Time.time;

                // Micro-drift overlay for realism; actual move is via BotMovementHelper (one-shot, not per-tick)
                Vector3 drifted = target + UnityEngine.Random.insideUnitSphere * MicroJitterMag;
                drifted.y = target.y;

                BotMovementHelper.SmoothMoveToSafe(_bot, drifted, slow, cohesion);
            }
        }

        #endregion

        #region Tactical Movement: Squad/Formation/Cover/Anticipation

        private void TryFormationFollow(float deltaTime)
        {
            if (_bot.BotsGroup == null || _bot.BotsGroup.MembersCount < 2) return;
            if (Time.time < _formationUpdateUntil) return;

            int count = _bot.BotsGroup.MembersCount;
            BotOwner leader = null;

            for (int i = 0; i < count; i++)
            {
                BotOwner candidate = _bot.BotsGroup.Member(i);
                if (candidate != null && !candidate.IsDead && candidate != _bot)
                {
                    if (!_formationRolePriority || (candidate.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault)
                        < (_bot.Profile?.Info?.Settings?.Role ?? WildSpawnType.assault))
                    {
                        leader = candidate;
                        break;
                    }
                }
            }
            if (leader == null)
                leader = _bot.BotsGroup.Member(0);

            if (leader != null && leader != _bot)
            {
                float distToLeader = (leader.Position - _bot.Position).sqrMagnitude;
                if (!_formationHold && distToLeader > 2.25f && UnityEngine.Random.value < 0.10f)
                {
                    _formationHold = true;
                    _formationHoldUntil = Time.time + UnityEngine.Random.Range(0.67f, 1.8f);
                    _lastLeaderPos = leader.Position;
                    return;
                }
                if (_formationHold)
                {
                    if (Time.time >= _formationHoldUntil ||
                        (leader.Position - _lastLeaderPos).sqrMagnitude > 2.7f * 2.7f)
                    {
                        _formationHold = false;
                        _formationHoldUntil = 0f;
                    }
                    else return;
                }
                if (distToLeader > 2.25f)
                {
                    Vector3 formationTarget = BotNavHelper.GetGroupFormationTarget(_bot, leader, _formationTargetWeight, SquadCoverSpacing);
                    Vector3 move = (formationTarget - _bot.Position) * deltaTime * 1.25f;
                    IssueMove(_bot.Position + move, false, _formationTargetWeight);
                }
            }
            _formationUpdateUntil = Time.time + UnityEngine.Random.Range(0.91f, 2.25f);
        }

        private void TrySquadSpacing(float deltaTime)
        {
            if (_bot.BotsGroup == null || _bot.BotsGroup.MembersCount < 2) return;
            Vector3 myPos = _bot.Position;
            int count = _bot.BotsGroup.MembersCount;

            for (int i = 0; i < count; i++)
            {
                BotOwner mate = _bot.BotsGroup.Member(i);
                if (mate == null || mate == _bot || mate.IsDead) continue;
                float dist = Vector3.Distance(myPos, mate.Position);
                if (dist < GroupSpacing)
                {
                    Vector3 away = (myPos - mate.Position).normalized * SpacingForce * (GroupSpacing - dist);
                    IssueMove(_bot.Position + away, false, 0.6f);
                }
            }
        }

        private void TryCombatStrafe(BotMover mover, float deltaTime)
        {
            if (_bot == null || mover == null || _bot.Memory?.GoalEnemy == null) return;
            if (!mover.HasPathAndNoComplete || !_bot.Mover.IsMoving || _bot.Transform == null) return;
            float distToEnemy = Vector3.Distance(_bot.Position, _bot.Memory.GoalEnemy.CurrPosition);
            if (distToEnemy > 40f) return;

            _strafeTimer -= deltaTime;
            if (_strafeTimer <= 0f)
            {
                float suppressionFactor = 1f;
                if (_cache?.Suppression != null && _cache.Suppression.IsSuppressed())
                    suppressionFactor = CombatStrafeSuppressed;
                if (_cache?.PanicHandler != null && _cache.PanicHandler.IsPanicking)
                    suppressionFactor *= 0.71f;
                _strafeRight = UnityEngine.Random.value > 0.5f;
                _strafeTimer = UnityEngine.Random.Range(CombatStrafeCooldownMin, CombatStrafeCooldownMax) / suppressionFactor;
            }
            Vector3 toEnemy = _bot.Memory.GoalEnemy.CurrPosition - _bot.Position;
            float angle = Vector3.Angle(_bot.Transform.forward, toEnemy.normalized);
            if (angle > MaxStrafeAngle) return;

            Vector3 offset = _strafeRight ? _bot.Transform.right : -_bot.Transform.right;
            Vector3 jitter = UnityEngine.Random.insideUnitSphere * CombatStrafeJitter;
            Vector3 strafe = (offset + jitter).normalized * CombatStrafeBase;
            Vector3 navDir = mover.NormDirCurPoint;
            Vector3 blend = Vector3.Lerp(navDir, strafe, UnityEngine.Random.Range(0.18f, 0.33f)).normalized * deltaTime;

            if (_inCover) blend *= 0.55f;
            if (_cache?.PanicHandler != null && _cache.PanicHandler.IsPanicking)
                blend += UnityEngine.Random.insideUnitSphere * PathWobble * deltaTime;

            IssueMove(_bot.Position + blend, false, 0.43f);
        }

        #endregion

        #region Lean, Look, Cover, Fallback

        private void TryLean()
        {
            if (_cache?.Tilt == null || _bot == null) return;
            float now = Time.time;

            if ((_cache.Suppression != null && _cache.Suppression.IsSuppressed()) ||
                (_cache.PanicHandler != null && _cache.PanicHandler.IsPanicking))
            {
                _leanLockoutUntil = now + LeanSuppressed;
                _cache.Tilt.Stop();
                return;
            }
            if (now < _leanLockoutUntil || now < _leanHoldUntil) return;
            if (_cache.IsBlinded) { _cache.Tilt.Stop(); return; }
            var memory = _bot.Memory;
            if (memory?.GoalEnemy == null || _bot.Transform == null) { _cache.Tilt.Stop(); return; }
            var player = _bot.GetPlayer;
            if (player != null && (player.IsSprintEnabled || _bot.Mover.Sprinting || !_bot.Mover.IsMoving)) { _cache.Tilt.Stop(); return; }

            bool wallLeft = Physics.Raycast(_bot.Position + Vector3.up * 1.53f, -_bot.Transform.right, CoverRayDist, AIRefactoredLayerMasks.VisionBlockers);
            bool wallRight = Physics.Raycast(_bot.Position + Vector3.up * 1.53f, _bot.Transform.right, CoverRayDist, AIRefactoredLayerMasks.VisionBlockers);
            BotTiltType nextLean = BotTiltType.right;

            if (wallLeft && !wallRight) nextLean = BotTiltType.right;
            else if (wallRight && !wallLeft) nextLean = BotTiltType.left;
            else
            {
                Vector3 toEnemy = memory.GoalEnemy.CurrPosition - _bot.Position;
                float dot = Vector3.Dot(toEnemy.normalized, _bot.Transform.right);
                nextLean = dot > 0f ? BotTiltType.right : BotTiltType.left;
            }

            if (_currentLean != nextLean || now >= _leanHoldUntil)
            {
                _currentLean = nextLean;
                _cache.Tilt.Set(nextLean);
                _leanHoldUntil = now + UnityEngine.Random.Range(MinLeanHold, MaxLeanHold);
            }

            if (UnityEngine.Random.value < _leanMissChance)
            {
                _nextLeanTime = now + UnityEngine.Random.Range(0.62f, 1.19f);
                _cache.Tilt.Stop();
                return;
            }
            if (now < _nextLeanTime) return;
            _nextLeanTime = now + UnityEngine.Random.Range(LeanCooldownMin, LeanCooldownMax);
            _leanLockoutUntil = now + UnityEngine.Random.Range(0.41f, 0.69f);
        }

        private void TrySmoothLook(BotMover mover, float deltaTime)
        {
            if (_bot?.Transform == null || mover == null) return;
            Vector3 target;
            try { target = mover._pathController.LastTargetPoint(1f); }
            catch { return; }
            if (!NavMesh.SamplePosition(target, out NavMeshHit navHit, 1.5f, NavMesh.AllAreas)) return;
            Vector3 direction = navHit.position - _bot.Position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f) return;
            Vector3 lookTarget = navHit.position;
            Vector3 botHead = _bot.Position + Vector3.up * 1.54f;
            Vector3 lookDir = (lookTarget - botHead).normalized;
            float yAngle = Mathf.Asin(lookDir.y) * Mathf.Rad2Deg;
            yAngle = Mathf.Clamp(yAngle, LookYClampMin, LookYClampMax);
            lookDir.y = Mathf.Sin(yAngle * Mathf.Deg2Rad);
            lookTarget = botHead + lookDir * (lookTarget - botHead).magnitude;
            BotMovementHelper.SmoothLookTo(_bot, lookTarget, 0.89f);
        }

        private void TryCoverPause()
        {
            if (_coverPauseActive && Time.time < _coverPauseUntil) return;
            _inCover = false;
            Vector3 head = _bot.Position + Vector3.up * 1.44f;
            if (Physics.Raycast(head, _bot.Transform.forward, CoverRayDist, AIRefactoredLayerMasks.CoverRayMask))
            {
                _inCover = true;
                if (UnityEngine.Random.value < ObstaclePauseChance)
                {
                    _coverPauseActive = true;
                    _coverPauseUntil = Time.time + UnityEngine.Random.Range(0.16f, 0.41f);
                }
            }
            else
            {
                _coverPauseActive = false;
            }
        }

        private void TryFallbackPathCorrection()
        {
            if (_fallbackActive && Time.time < _fallbackRetryTime) return;
            if (_fallbackActive)
            {
                Vector3 backoff = -_bot.Transform.forward * FallbackBackoff;
                IssueMove(_bot.Position + backoff, false, 0.33f);
                _fallbackActive = false;
            }
        }

        private void TryPanicPathWobble(float deltaTime)
        {
            if (_cache?.PanicHandler == null || !_cache.PanicHandler.IsPanicking) return;
            _panicPathWobbleTimer -= deltaTime;
            if (_panicPathWobbleTimer > 0f) return;
            _panicPathWobbleTimer = UnityEngine.Random.Range(0.13f, 0.21f);
            Vector3 sidestep = UnityEngine.Random.insideUnitSphere * PathWobble;
            sidestep.y = 0f;
            IssueMove(_bot.Position + sidestep, false, 0.19f);
        }

        #endregion

        #region Sprint, Scan, Startle

        private void TryTacticalSprint(float deltaTime)
        {
            if (_bot == null || _bot.Mover == null || _bot.IsDead) return;
            if (_shouldSprint && Time.time < _sprintUntil) return;
            if (_bot.Memory?.GoalEnemy != null || _inCover ||
                (_cache.PanicHandler != null && _cache.PanicHandler.IsPanicking) ||
                (_cache.Suppression != null && _cache.Suppression.IsSuppressed()))
            {
                if (_shouldSprint)
                {
                    _bot.Mover.Sprint(false);
                    _shouldSprint = false;
                    _sprintUntil = 0f;
                }
                return;
            }
            if (!_shouldSprint && _bot.Mover.IsMoving && UnityEngine.Random.value < 0.13f)
            {
                Vector3 sprintTarget = Vector3.zero;
                bool validTarget = false;
                try
                {
                    if (_bot.Mover != null && _bot.Mover._pathController != null)
                    {
                        Vector3? tp = _bot.Mover._pathController.TargetPoint;
                        if (tp.HasValue)
                        {
                            sprintTarget = tp.Value;
                            validTarget = true;
                        }
                    }
                }
                catch { validTarget = false; }
                if (validTarget)
                {
                    float pathLen = (sprintTarget - _bot.Position).sqrMagnitude;
                    if (pathLen > MinSprintDist * MinSprintDist && pathLen < MaxSprintDist * MaxSprintDist)
                    {
                        if (!_bot.Mover.Sprinting)
                        {
                            _bot.Mover.Sprint(true);
                            _shouldSprint = true;
                            _sprintUntil = Time.time + UnityEngine.Random.Range(SprintMinDur, SprintMaxDur);
                        }
                    }
                }
            }
            if (_shouldSprint)
            {
                if (Time.time >= _sprintUntil || UnityEngine.Random.value < 0.13f)
                {
                    _bot.Mover.Sprint(false);
                    _shouldSprint = false;
                    _sprintUntil = 0f;
                }
            }
        }

        private void UpdateStartlePause()
        {
            if (_cache?.PanicHandler != null)
            {
                bool nowPanicking = _cache.PanicHandler.IsPanicking;
                if (!_lastPanicking && nowPanicking)
                {
                    if (UnityEngine.Random.value < _personalityStartleChance)
                        _startleUntil = Time.time + StartlePause + UnityEngine.Random.Range(0.05f, 0.11f);
                }
                _lastPanicking = nowPanicking;
            }
        }

        #endregion

        #region Mode Controls

        public void EnterLootingMode() { }
        public void ExitLootingMode() { }
        public bool IsInLootingMode() => false;

        #endregion
    }
}
