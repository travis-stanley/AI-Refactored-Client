#nullable enable

namespace AIRefactored.AI.Optimization
{
    using System.Linq;

    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Hotspots;
    using AIRefactored.AI.Navigation;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Unified resolver that combines NavPoints, hotspots, cover scoring,
    ///     and fallback path planning to find realistic escape destinations.
    /// </summary>
    public static class HybridFallbackResolver
    {
        /// <summary>
        ///     Resolves the best possible retreat point using multiple strategies in order of priority.
        /// </summary>
        /// <param name="bot">The bot seeking to retreat.</param>
        /// <param name="threatDirection">Direction of known threat or enemy.</param>
        public static Vector3? GetBestRetreatPoint(BotOwner bot, Vector3 threatDirection)
        {
            var origin = bot.Position;

            // === Priority 1: High-quality cover from NavPoints ===
            var navCoverPoints = NavPointRegistry.QueryNearby(
                origin,
                30f,
                (Vector3 pos) => NavPointRegistry.IsCoverPoint(pos) && Vector3.Dot(
                                     (pos - origin).normalized,
                                     -threatDirection.normalized) > 0.4f,
                true);

            if (navCoverPoints.Count > 0)
            {
                var best = navCoverPoints
                    .Select((Vector3 pos) => new
                                                 {
                                                     Position = pos,
                                                     Score = CoverScorer.ScoreCoverPoint(bot, pos, threatDirection)
                                                 }).OrderByDescending((p) => p.Score).First().Position;

                return best;
            }

            // === Priority 2: Tactical fallback-type Hotspots ===
            var fallbackHotspots = HotspotRegistry.QueryNearby(
                origin,
                40f,
                (HotspotRegistry.Hotspot h) => Vector3.Dot(
                                                   (h.Position - origin).normalized,
                                                   -threatDirection.normalized) > 0.5f);

            if (fallbackHotspots.Count > 0)
                return fallbackHotspots.OrderBy((HotspotRegistry.Hotspot h) => Vector3.Distance(origin, h.Position))
                    .First().Position;

            // === Priority 3: Dynamic path-based fallback planner ===
            var pathCache = BotCacheUtility.GetCache(bot)?.PathCache;
            if (pathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(bot, threatDirection, pathCache);
                if (path.Count >= 2)
                    return path[path.Count - 1];
            }

            // === Priority 4: Line-of-sight blocker object ===
            if (TryLOSBlocker(origin, threatDirection, out var losPoint))
                return losPoint;

            // No retreat point found
            return null;
        }

        /// <summary>
        ///     Attempts to find a last-resort fallback that simply breaks line-of-sight.
        /// </summary>
        private static bool TryLOSBlocker(Vector3 origin, Vector3 threatDir, out Vector3 result)
        {
            var dir = -threatDir.normalized;
            var eye = origin + Vector3.up * 1.5f;

            const float maxSearchDist = 12f;

            for (var dist = 2f; dist <= maxSearchDist; dist += 1.5f)
            {
                var probe = origin + dir * dist + Vector3.up * 1.5f;

                if (Physics.Raycast(probe, threatDir, out var hit, 20f))
                {
                    if (!CoverScorer.IsSolid(hit.collider))
                        continue;

                    result = hit.point - threatDir.normalized * 1.0f;
                    return true;
                }
            }

            result = Vector3.zero;
            return false;
        }
    }
}