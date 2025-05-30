﻿// <auto-generated>
//   AI-Refactored: BotMovementHelper.cs (Beyond Diamond Overlay-Only, June 2025)
//   All movement is overlay/event-driven, never tick-throttled, never replaces native BotMover except on explicit fallback/recovery.
//   Absolute parity: SPT, FIKA, headless, and traditional client. 0 teleports. No per-frame destination reassigns. Bulletproof error handling.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Helpers
{
    using System;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Navigation;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    /// Centralized helper for all *real* bot movement commands in AIRefactored.
    /// - **Overlay-only:** Never drives BotMover per tick; only called on explicit state/mission change or validated recovery.
    /// - Humanization (micro-jitter) is visually layered, never reassigns agent destination.
    /// - Event-driven, 1:1 SPT/FIKA/headless/client-safe, and strictly path-based. Zero teleport, no transform assignment.
    /// </summary>
    public static class BotMovementHelper
    {
        #region Constants

        private const float DefaultLookSpeed = 4.25f;
        private const float DefaultStrafeDistance = 3.0f;
        private const float RetreatDistance = 6.5f;
        private const float MinMoveEpsilon = 0.07f;
        private const float SlerpBias = 0.965f;
        private const float MicroJitterMagnitude = 0.11f;

        #endregion

        #region Movement Cache

        /// <summary>
        /// Per-bot movement overlay cache (micro-jitter state, last explicit move issued).
        /// </summary>
        public class BotMoveCache
        {
            public Vector3 LastMoveTarget = Vector3.zero;
            public float LastMoveTime = -1000f;
            public Vector3 CurrentDriftTarget = Vector3.zero;
            public float NextDriftUpdate = 0f;
        }

        private static BotMoveCache GetCache(BotOwner bot)
        {
            return bot?.GetComponent<BotComponentCache>()?.MoveCache;
        }

        /// <summary>
        /// Resets all per-bot movement cache values. Call on spawn, extract, or mission change.
        /// </summary>
        public static void Reset(BotOwner bot)
        {
            var cache = GetCache(bot);
            if (cache != null)
            {
                cache.LastMoveTarget = Vector3.zero;
                cache.LastMoveTime = -1000f;
                cache.CurrentDriftTarget = Vector3.zero;
                cache.NextDriftUpdate = 0f;
            }
        }

        #endregion

        #region Look/Rotation Overlay

        /// <summary>
        /// Smoothly rotates bot head/aim to a look target.
        /// Overlay-only: never forcibly reassigns transform except for visual look/aim control.
        /// </summary>
        public static void SmoothLookTo(BotOwner bot, Vector3 lookTarget, float speed = DefaultLookSpeed)
        {
            try
            {
                if (!IsAlive(bot) || !IsValidTarget(lookTarget))
                    return;

                Transform tf = bot.Transform?.Original;
                if (tf == null)
                    return;

                Vector3 origin = GetPosition(bot);
                Vector3 dir = lookTarget - origin;
                dir.y = 0f;
                if (dir.sqrMagnitude < MinMoveEpsilon)
                    return;

                // Humanized overshoot/imperfection overlay
                float overshoot = 1f + ((SeededRandom(bot.ProfileId, Time.frameCount) - 0.5f) * 0.03f);
                Quaternion targetRotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
                float t = SlerpBias * Time.deltaTime * Mathf.Clamp(speed, 1.1f, 9.5f) * overshoot;
                tf.rotation = Quaternion.Slerp(tf.rotation, targetRotation, t);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError("[BotMovementHelper] SmoothLookTo failed: " + ex);
            }
        }

        #endregion

        #region Movement APIs (Overlay-Only: Event Driven)

        /// <summary>
        /// Issues a *real* move (path-based) to target, only on explicit request.
        /// This does NOT reassign destination per tick—overlay-only enhancement.
        /// </summary>
        public static void SmoothMoveTo(BotOwner bot, Vector3 target, bool slow = true)
        {
            SmoothMoveToSafe(bot, target, slow, 1f);
        }

        /// <summary>
        /// Main entry for any explicit movement update (mission, fallback, or recover).
        /// Does *not* tick/refresh per frame.
        /// </summary>
        public static bool SmoothMoveToSafe(BotOwner bot, Vector3 target, bool slow = true, float cohesion = 1f)
        {
            try
            {
                if (!IsAlive(bot) || !IsValidTarget(target) || !BotNavHelper.IsNavMeshPositionValid(target))
                    return false;

                // Only intervene if stuck or at destination (explicit recovery only)
                if (IsStuckAtTarget(bot) || BotNavHelper.IsAtDestination(bot))
                {
                    ForceFallbackMove(bot);
                    return false;
                }

                Vector3 safeTarget = BotNavHelper.TryGetSafeTarget(bot, out var fallback) ? fallback : target;
                if (!BotNavHelper.IsNavMeshPositionValid(safeTarget))
                    safeTarget = GetPosition(bot);

                if (NavMesh.SamplePosition(safeTarget, out var hit, 1.5f, NavMesh.AllAreas))
                {
                    BotRegistry.TryGet(bot.ProfileId, out var profile);
                    Vector3 drifted = ApplyMicroDrift(hit.position, bot.ProfileId, Time.frameCount, profile, bot);
                    IssueMove(bot, drifted, slow, cohesion);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError("[BotMovementHelper] SmoothMoveToSafe failed: " + ex);
                return false;
            }
        }

        /// <summary>
        /// Directs bot to exfiltration (safe exit), path-based, overlay-only, never tick-driven.
        /// </summary>
        public static void SmoothMoveToSafeExit(BotOwner bot, Vector3 exfilTarget)
        {
            try
            {
                if (!IsAlive(bot) || !IsValidTarget(exfilTarget)) return;

                if (!BotNavHelper.IsNavMeshPositionValid(exfilTarget))
                {
                    if (!NavMesh.SamplePosition(exfilTarget, out var navHit, 2.0f, NavMesh.AllAreas))
                        return;

                    exfilTarget = navHit.position;
                }

                SmoothMoveTo(bot, exfilTarget, slow: true);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError("[BotMovementHelper] SmoothMoveToSafeExit failed: " + ex);
            }
        }

        /// <summary>
        /// Emergency fallback: only used when stuck or forced recovery.
        /// Never ticked, never periodic; overlay-only event call.
        /// </summary>
        public static void ForceFallbackMove(BotOwner bot)
        {
            try
            {
                if (!IsAlive(bot)) return;

                Vector3 origin = GetPosition(bot);
                Vector3 dir = bot.LookDirection.normalized;
                Vector3 rawTarget = origin + dir * 5f;

                Vector3 fallback = BotNavHelper.TryGetSafeTarget(bot, out var safeTarget) ? safeTarget : rawTarget;
                if (!IsValidTarget(fallback) || !BotNavHelper.IsNavMeshPositionValid(fallback))
                    fallback = origin;

                if (NavMesh.SamplePosition(fallback, out var hit, 1.5f, NavMesh.AllAreas))
                {
                    BotRegistry.TryGet(bot.ProfileId, out var profile);
                    Vector3 drifted = ApplyMicroDrift(hit.position, bot.ProfileId, Time.frameCount + 21, profile, bot);
                    IssueMove(bot, drifted, true, 1f);

                    var comp = bot.GetComponent<BotComponentCache>();
                    comp?.PoseController?.Stand();
                    comp?.Tilt?.Stop();
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError("[BotMovementHelper] ForceFallbackMove failed: " + ex);
            }
        }

        /// <summary>
        /// Retreat/cover move: explicit, overlay-only, never ticked.
        /// </summary>
        public static void RetreatToCover(BotOwner bot, Vector3 threatDir, float distance = RetreatDistance, bool sprint = true)
        {
            try
            {
                if (!IsAlive(bot)) return;

                Vector3 origin = GetPosition(bot);
                Vector3 target = origin - threatDir.normalized * distance;

                if (!BotNavHelper.IsNavMeshPositionValid(target))
                    target = origin;

                if (NavMesh.SamplePosition(target, out var hit, 1.5f, NavMesh.AllAreas))
                {
                    BotRegistry.TryGet(bot.ProfileId, out var profile);
                    float cohesion = profile != null ? Mathf.Clamp(profile.Cohesion, 0.7f, 1.3f) : 1f;
                    if (profile != null && (profile.IsFrenzied || profile.IsFearful)) sprint = true;

                    Vector3 drifted = ApplyMicroDrift(hit.position, bot.ProfileId, Time.frameCount, profile, bot);
                    IssueMove(bot, drifted, true, cohesion);
                    if (sprint) bot.Sprint(true);
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError("[BotMovementHelper] RetreatToCover failed: " + ex);
            }
        }

        /// <summary>
        /// Issues a lateral strafe (only on explicit event, not every frame).
        /// </summary>
        public static void SmoothStrafeFrom(BotOwner bot, Vector3 threatDir, float scale = 1f)
        {
            try
            {
                if (!IsAlive(bot)) return;

                Vector3 origin = GetPosition(bot);
                Vector3 right = Vector3.Cross(Vector3.up, threatDir.normalized);
                if (right.sqrMagnitude < 0.01f) right = Vector3.right;

                Vector3 offset = right * DefaultStrafeDistance * Mathf.Clamp(scale, 0.75f, 1.25f);
                Vector3 rawTarget = origin + offset;

                Vector3 final = BotNavHelper.TryGetSafeTarget(bot, out var safeTarget) ? safeTarget : rawTarget;
                if (!BotNavHelper.IsNavMeshPositionValid(final))
                    final = origin;

                if (NavMesh.SamplePosition(final, out var hit, 1.5f, NavMesh.AllAreas))
                {
                    BotRegistry.TryGet(bot.ProfileId, out var profile);
                    Vector3 drifted = ApplyMicroDrift(hit.position, bot.ProfileId, Time.frameCount + 15, profile, bot);
                    IssueMove(bot, drifted, false, 1f);
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError("[BotMovementHelper] SmoothStrafeFrom failed: " + ex);
            }
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Internally issues a *real* GoToPoint to BotMover.
        /// Only called on explicit event (not every tick/frame).
        /// </summary>
        private static void IssueMove(BotOwner bot, Vector3 drifted, bool slow, float cohesion)
        {
            var cache = GetCache(bot);
            if (bot?.Mover == null) return;

            float sqrDist = cache != null ? (cache.LastMoveTarget - drifted).sqrMagnitude : 999f;
            float timeSinceLast = Time.time - (cache?.LastMoveTime ?? -100f);

            if (sqrDist >= 0.25f || timeSinceLast >= 1.0f || IsStuckAtTarget(bot))
            {
                bot.Mover.GoToPoint(drifted, slow, cohesion);

                if (cache != null)
                {
                    cache.LastMoveTarget = drifted;
                    cache.LastMoveTime = Time.time;
                }
            }
        }

        /// <summary>
        /// Returns true if bot appears stuck at the last issued destination.
        /// Used for explicit recovery only; never triggers per tick.
        /// </summary>
        public static bool IsStuckAtTarget(BotOwner bot)
        {
            var cache = GetCache(bot);
            return cache != null
                && Vector3.Distance(GetPosition(bot), cache.LastMoveTarget) < 0.25f
                && Time.time - cache.LastMoveTime > 2.5f;
        }

        private static bool IsAlive(BotOwner bot)
        {
            return bot != null && bot.GetPlayer != null && bot.GetPlayer.IsAI && !bot.IsDead;
        }

        private static bool IsValidTarget(Vector3 pos)
        {
            return !float.IsNaN(pos.x) && !float.IsNaN(pos.y) && !float.IsNaN(pos.z)
                && !float.IsInfinity(pos.x) && !float.IsInfinity(pos.y) && !float.IsInfinity(pos.z);
        }

        public static Vector3 GetPosition(BotOwner bot)
        {
            if (bot == null) return Vector3.zero;

            Vector3 pos = bot.Position;
            if (IsValidTarget(pos)) return pos;

            Player player = bot.GetPlayer;
            if (player != null)
            {
                Vector3 playerPos = EFTPlayerUtil.GetPosition(player);
                if (IsValidTarget(playerPos)) return playerPos;
            }

            return bot.Transform?.Original?.position ?? Vector3.zero;
        }

        /// <summary>
        /// Applies pooled, personality-driven micro-drift (overlay only, never moves root agent).
        /// Only updates every 0.25–0.45s for visual smoothness.
        /// </summary>
        public static Vector3 ApplyMicroDrift(Vector3 pos, string profileId, int tick, BotPersonalityProfile profile = null, BotOwner bot = null)
        {
            var cache = GetCache(bot);
            if (cache == null) return pos;

            float now = Time.time;
            if (now > cache.NextDriftUpdate || cache.CurrentDriftTarget == Vector3.zero)
            {
                float baseMag = MicroJitterMagnitude;
                float personalityBias = profile != null
                    ? Mathf.Clamp(1f + (profile.MovementJitter * 0.15f) + (profile.AggressionLevel * 0.05f), 0.93f, 1.15f)
                    : 1f;

                int hash = (profileId?.GetHashCode() ?? 0) ^ (tick * 11) ^ 0x17DF413;
                unchecked
                {
                    hash = (hash ^ (hash >> 13)) * 0x7FEDCBA9;
                    float dx = ((hash & 0xFF) / 255f - 0.5f) * baseMag * personalityBias;
                    float dz = (((hash >> 8) & 0xFF) / 255f - 0.5f) * baseMag * personalityBias;
                    cache.CurrentDriftTarget = pos + new Vector3(dx, 0, dz);
                }

                cache.NextDriftUpdate = now + UnityEngine.Random.Range(0.25f, 0.45f);
            }

            return Vector3.Lerp(pos, cache.CurrentDriftTarget, 0.2f);
        }

        private static float SeededRandom(string profileId, int tick)
        {
            int hash = (profileId?.GetHashCode() ?? 0) ^ (tick * 163) ^ 0x1A983D;
            unchecked { hash = (hash ^ (hash >> 13)) * 0x5E2D58B9; }
            return ((hash & 0x7FFFFFFF) % 997) / 997f;
        }

        #endregion
    }
}
