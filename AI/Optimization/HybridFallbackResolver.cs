﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

#nullable enable

namespace AIRefactored.AI.Optimization
{
    using System.Collections.Generic;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Hotspots;
    using AIRefactored.AI.Navigation;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Unified resolver that combines NavPoints, hotspots, cover scoring,
    /// and fallback path planning to find realistic escape destinations.
    /// </summary>
    public static class HybridFallbackResolver
    {
        #region Constants

        private const float NavpointSearchRadius = 30f;
        private const float HotspotSearchRadius = 40f;
        private const float MinDotCover = 0.4f;
        private const float MinDotHotspot = 0.5f;

        #endregion

        #region Public API

        /// <summary>
        /// Resolves the best possible retreat point using multiple strategies in order of priority.
        /// </summary>
        /// <param name="bot">The bot evaluating retreat options.</param>
        /// <param name="threatDirection">Direction from which the threat is coming.</param>
        /// <returns>The most suitable fallback point, or null if none is found.</returns>
        public static Vector3? GetBestRetreatPoint(BotOwner bot, Vector3 threatDirection)
        {
            if (bot == null || !GameWorldHandler.IsLocalHost())
            {
                return null;
            }

            Vector3 origin = bot.Position;
            Vector3 retreatDirection = -threatDirection.normalized;

            // === Priority 1: NavPoint-based cover ===
            List<Vector3> navCoverPoints = NavPointRegistry.QueryNearby(
                origin,
                NavpointSearchRadius,
                delegate (Vector3 pos)
                {
                    Vector3 toCandidate = (pos - origin).normalized;
                    return NavPointRegistry.IsCoverPoint(pos) &&
                           Vector3.Dot(toCandidate, retreatDirection) > MinDotCover;
                },
                true);

            if (navCoverPoints.Count > 0)
            {
                Vector3 bestPoint = Vector3.zero;
                float bestScore = float.MinValue;

                for (int i = 0; i < navCoverPoints.Count; i++)
                {
                    float score = CoverScorer.ScoreCoverPoint(bot, navCoverPoints[i], threatDirection);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPoint = navCoverPoints[i];
                    }
                }

                return bestPoint;
            }

            // === Priority 2: Hotspot fallback zones ===
            List<HotspotRegistry.Hotspot> fallbackHotspots = HotspotRegistry.QueryNearby(
                origin,
                HotspotSearchRadius,
                delegate (HotspotRegistry.Hotspot h)
                {
                    Vector3 toHotspot = (h.Position - origin).normalized;
                    return Vector3.Dot(toHotspot, retreatDirection) > MinDotHotspot;
                });

            if (fallbackHotspots.Count > 0)
            {
                Vector3 closest = Vector3.zero;
                float minDist = float.MaxValue;

                for (int i = 0; i < fallbackHotspots.Count; i++)
                {
                    float dist = Vector3.Distance(origin, fallbackHotspots[i].Position);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        closest = fallbackHotspots[i].Position;
                    }
                }

                return closest;
            }

            // === Priority 3: Dynamic fallback path ===
            BotOwnerPathfindingCache? pathCache = BotCacheUtility.GetCache(bot)?.Pathing;
            if (pathCache != null)
            {
                List<Vector3> path = BotCoverRetreatPlanner.GetCoverRetreatPath(bot, threatDirection, pathCache);
                if (path != null && path.Count >= 2)
                {
                    return path[path.Count - 1];
                }
            }

            // === Priority 4: LOS-blocking fallback ===
            Vector3 losBreak;
            if (TryLOSBlocker(origin, threatDirection, out losBreak))
            {
                return losBreak;
            }

            return null;
        }

        #endregion

        #region Private Logic

        /// <summary>
        /// Attempts to find a line-of-sight blocking position by raycasting behind the bot.
        /// </summary>
        /// <param name="origin">Bot's current position.</param>
        /// <param name="threatDir">Direction from which threat is expected.</param>
        /// <param name="result">Final position if successful.</param>
        /// <returns>True if a valid LOS blocker was found.</returns>
        private static bool TryLOSBlocker(Vector3 origin, Vector3 threatDir, out Vector3 result)
        {
            const float EyeHeight = 1.5f;
            const float MaxSearchDist = 12f;
            const float StepSize = 1.5f;

            Vector3 backwards = -threatDir.normalized;
            Vector3 eyeOrigin = origin + Vector3.up * EyeHeight;

            for (float dist = 2f; dist <= MaxSearchDist; dist += StepSize)
            {
                Vector3 probe = origin + backwards * dist + Vector3.up * EyeHeight;

                if (Physics.Raycast(
                        probe,
                        threatDir,
                        out RaycastHit hit,
                        20f,
                        AIRefactoredLayerMasks.VisionBlockers))
                {
                    if (CoverScorer.IsSolid(hit.collider))
                    {
                        result = hit.point - threatDir.normalized * 1f;
                        return true;
                    }
                }
            }

            result = Vector3.zero;
            return false;
        }

        #endregion
    }
}
