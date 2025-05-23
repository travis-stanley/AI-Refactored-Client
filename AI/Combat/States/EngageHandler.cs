﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Bulletproof: All errors are locally isolated, never disables itself, never triggers fallback AI.
//   Anti-teleport: All movement is smooth, validated, and strictly path-based.
//   Polish: Cautious advance, micro-pauses, anti-cluster squad pathing, and human-like “give up” logic.
// </auto-generated>

namespace AIRefactored.AI.Combat.States
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Navigation;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Guides tactical movement toward enemy's last known location.
    /// Supports cautious advancement, micro-pauses, anti-cluster squad pathing, and personality-driven approach.
    /// All movement is path-based only—teleportation is strictly forbidden.
    /// Bulletproof: all failures are isolated; never disables itself or squadmates.
    /// </summary>
    public sealed class EngageHandler
    {
        #region Constants

        private const float DefaultEngagementRange = 25.0f;
        private const float MinAdvanceDelay = 0.06f;
        private const float MaxAdvanceDelay = 0.17f;
        private const float MaxNavSampleRadius = 1.6f;
        private const float MaxAdvanceDistance = 16.2f;
        private const float AdvanceSmoothing = 4.3f;
        private const float IdleScanPauseMin = 0.21f;
        private const float IdleScanPauseMax = 0.54f;
        private const float GiveUpDistance = 1.25f;
        private const float LookScanVariance = 0.38f;

        #endregion

        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly float _fallbackRange;
        private float _lastAdvanceTime;
        private Vector3 _lastMoveDir;
        private float _lastIdlePause;
        private float _idlePauseUntil;
        private bool _hasGivenUp;

        #endregion

        #region Constructor

        public EngageHandler(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache?.Bot;
            float profileRange = cache?.PersonalityProfile?.EngagementRange ?? 0f;
            _fallbackRange = profileRange > 0f ? profileRange : DefaultEngagementRange;
            _lastAdvanceTime = -1000f;
            _lastMoveDir = Vector3.zero;
            _lastIdlePause = 0f;
            _idlePauseUntil = 0f;
            _hasGivenUp = false;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Returns true if the bot should advance towards the last known enemy position.
        /// </summary>
        public bool ShallUseNow()
        {
            return IsCombatCapable() && TryGetLastKnownEnemy(out Vector3 pos) && !IsWithinRange(pos) && !_hasGivenUp;
        }

        /// <summary>
        /// Returns true if the bot is close enough to attack.
        /// </summary>
        public bool CanAttack()
        {
            return IsCombatCapable() && TryGetLastKnownEnemy(out Vector3 pos) && IsWithinRange(pos) && !_hasGivenUp;
        }

        /// <summary>
        /// Returns true if the bot is currently engaging.
        /// </summary>
        public bool IsEngaging()
        {
            return IsCombatCapable() && TryGetLastKnownEnemy(out Vector3 pos) && !IsWithinRange(pos) && !_hasGivenUp;
        }

        /// <summary>
        /// Advances smoothly and human-like towards the last known enemy location.
        /// Squad-aware, personality-driven, anti-cluster, with micro-pauses and “give up” logic.
        /// </summary>
        public void Tick()
        {
            if (!IsCombatCapable())
                return;

            try
            {
                if (!TryGetLastKnownEnemy(out Vector3 enemyPos))
                    return;

                float now = Time.time;
                float hesitation = UnityEngine.Random.Range(MinAdvanceDelay, MaxAdvanceDelay);
                float personalityJitter = _cache.PersonalityProfile != null
                    ? UnityEngine.Random.Range(-_cache.PersonalityProfile.MovementJitter * 0.07f, _cache.PersonalityProfile.MovementJitter * 0.09f)
                    : 0f;

                if (now - _lastAdvanceTime < hesitation + personalityJitter)
                    return;

                // Already at/very close to the spot: idle, scan, or give up
                float giveUpDist = (_bot.Position - enemyPos).magnitude;
                if (giveUpDist < GiveUpDistance)
                {
                    // Only pause for a randomized idle scan if not already paused
                    if (_idlePauseUntil < now)
                    {
                        _lastIdlePause = UnityEngine.Random.Range(IdleScanPauseMin, IdleScanPauseMax);
                        _idlePauseUntil = now + _lastIdlePause;
                        _hasGivenUp = UnityEngine.Random.value < 0.27f + (_cache.PersonalityProfile?.Caution ?? 0.05f); // More cautious = higher give up
                        if (_hasGivenUp && _bot.BotTalk != null && !FikaHeadlessDetector.IsHeadless)
                        {
                            try { _bot.BotTalk.TrySay(EPhraseTrigger.Clear); } catch { }
                        }
                    }
                    // Optionally: perform idle look scan to random directions for a short while
                    if (UnityEngine.Random.value < 0.6f)
                        SmoothIdleLookScan();
                    return;
                }

                _lastAdvanceTime = now;

                Vector3 destination = enemyPos;

                // Squad micro-offset for anti-cluster, personality-randomized approach
                if (_cache.SquadPath != null)
                {
                    try { destination = _cache.SquadPath.ApplyOffsetTo(enemyPos); }
                    catch { destination = enemyPos; }
                }
                else
                {
                    // If solo, add slight personality-driven micro-offset
                    float soloOffsetMag = UnityEngine.Random.Range(-LookScanVariance, LookScanVariance);
                    Vector3 offsetDir = Vector3.Cross(Vector3.up, _bot.LookDirection.normalized);
                    destination += offsetDir * soloOffsetMag;
                }

                // Validate on navmesh, apply smoothing/micro-drift
                Vector3 safeDest = GetNavMeshSafeDestination(_bot.Position, destination);
                if (!IsValid(safeDest))
                    return;

                float advanceSqr = (safeDest - _bot.Position).sqrMagnitude;
                if (_bot.Mover != null && advanceSqr < (MaxAdvanceDistance * MaxAdvanceDistance))
                {
                    // Humanize: blend direction for smooth entry, never snap/teleport
                    Vector3 moveDir = safeDest - _bot.Position;
                    moveDir.y = 0f;

                    if (moveDir.sqrMagnitude > 0.01f)
                    {
                        if (_lastMoveDir == Vector3.zero)
                            _lastMoveDir = moveDir.normalized;

                        float blendT = Mathf.Clamp01(Time.deltaTime * AdvanceSmoothing);
                        Vector3 blended = Vector3.Lerp(_lastMoveDir, moveDir.normalized, blendT).normalized;
                        _lastMoveDir = blended;

                        // Personality-driven cohesion for squad flow
                        float cohesion = 1.0f;
                        if (_cache?.PersonalityProfile != null)
                            cohesion = Mathf.Clamp(_cache.PersonalityProfile.Cohesion, 0.7f, 1.3f);

                        // Apply micro drift for human shuffle
                        Vector3 humanizedTarget = _bot.Position + blended * moveDir.magnitude;
                        humanizedTarget = BotMovementHelper.ApplyMicroDrift(
                            humanizedTarget, _bot.ProfileId, Time.frameCount, _cache.PersonalityProfile);

                        BotMovementHelper.SmoothMoveTo(_bot, humanizedTarget, false, cohesion);

                        // Dynamic stance: if in cover or close to enemy, crouch or scan, otherwise stand
                        if (_cache.PoseController != null)
                        {
                            if (advanceSqr < 9.0f || _cache.PersonalityProfile?.Caution > 0.25f)
                                _cache.PoseController.Crouch();
                            else if (_cache.PersonalityProfile?.AggressionLevel > 0.7f)
                                _cache.PoseController.Stand();
                            else
                                _cache.PoseController.TrySetStanceFromNearbyCover(safeDest);
                        }

                        // Occasionally signal to squad (contextual voice comm)
                        if (!FikaHeadlessDetector.IsHeadless && _bot.BotTalk != null && UnityEngine.Random.value < 0.08f)
                        {
                            try { _bot.BotTalk.TrySay(EPhraseTrigger.GoForward); } catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[EngageHandler] Tick error: {ex}");
            }
        }

        #endregion

        #region Internal Helpers

        private bool IsCombatCapable()
        {
            return _bot != null && _cache != null && _cache.Combat != null;
        }

        private bool TryGetLastKnownEnemy(out Vector3 result)
        {
            result = _cache?.Combat?.LastKnownEnemyPos ?? Vector3.zero;
            return IsValid(result) && result != Vector3.zero;
        }

        private bool IsWithinRange(Vector3 enemyPos)
        {
            return (_bot.Position - enemyPos).sqrMagnitude < (_fallbackRange * _fallbackRange);
        }

        private static bool IsValid(Vector3 pos)
        {
            return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z) && pos != Vector3.zero;
        }

        private static Vector3 GetNavMeshSafeDestination(Vector3 current, Vector3 candidate)
        {
            UnityEngine.AI.NavMeshHit navHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out navHit, MaxNavSampleRadius, UnityEngine.AI.NavMesh.AllAreas))
            {
                float dist = (navHit.position - current).magnitude;
                if (dist < 0.08f || dist > MaxAdvanceDistance)
                    return current;
                return navHit.position;
            }
            return current;
        }

        /// <summary>
        /// Performs a smooth idle scan/look-around with micro-head-movements, no snapping.
        /// </summary>
        private void SmoothIdleLookScan()
        {
            if (_bot == null || _bot.Transform == null || _bot.IsDead)
                return;

            Vector3 scanOffset = UnityEngine.Random.insideUnitSphere * 1.6f;
            scanOffset.y = 0f;
            Vector3 lookTarget = _bot.Position + _bot.LookDirection.normalized * 2.8f + scanOffset;
            BotMovementHelper.SmoothLookTo(_bot, lookTarget, 2.9f + UnityEngine.Random.value * 1.6f);
        }

        #endregion
    }
}
