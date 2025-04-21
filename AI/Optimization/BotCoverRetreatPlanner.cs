#nullable enable

using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.Core;
using EFT;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Computes and caches dynamic retreat paths for bots under threat.
    /// Uses NavMesh sampling, danger zone avoidance, squad-based caching, and cover desirability scoring.
    /// </summary>
    public static class BotCoverRetreatPlanner
    {
        #region Config

        private const float RetreatDistance = 12f;
        private const int MaxSamples = 10;
        private const float NavSampleRadius = 2f;
        private const float MinSpacing = 3f;
        private const float DangerZonePenalty = 0.6f;
        private const float MemoryClearInterval = 60f;

        #endregion

        #region Cache

        private static readonly Dictionary<string, NavMeshSurface> MapNavSurfaces = new Dictionary<string, NavMeshSurface>();
        private static readonly Dictionary<string, Dictionary<string, List<Vector3>>> SquadRetreatCache = new Dictionary<string, Dictionary<string, List<Vector3>>>();
        private static float _lastClearTime = -99f;

        #endregion

        #region Public API

        /// <summary>
        /// Computes a safe fallback path for a bot under threat using local NavMesh samples.
        /// Applies squad-level caching and filters danger zones.
        /// </summary>
        /// <param name="bot">Bot owner to calculate path for.</param>
        /// <param name="threatDir">Direction from which threat is perceived.</param>
        /// <param name="pathCache">Local pathfinding cache reference.</param>
        /// <returns>List of waypoints forming a retreat path.</returns>
        public static List<Vector3> GetCoverRetreatPath(BotOwner bot, Vector3 threatDir, BotOwnerPathfindingCache pathCache)
        {
            if (!IsAIBot(bot) || bot.Transform == null)
                return new List<Vector3>();

            ClearExpiredCache();

            string map = GameWorldHandler.GetCurrentMapName();
            string squadId = bot.Profile?.Info?.GroupId ?? bot.ProfileId;

            if (!SquadRetreatCache.TryGetValue(map, out var squadCache))
            {
                squadCache = new Dictionary<string, List<Vector3>>();
                SquadRetreatCache[map] = squadCache;
            }

            if (squadCache.TryGetValue(squadId, out var cachedPath) && cachedPath.Count >= 2)
                return cachedPath;

            EnsureNavMeshAvailable(map);

            var profile = BotRegistry.Get(bot.ProfileId);
            Vector3 origin = bot.Position;
            Vector3 away = -threatDir.normalized;

            float composure = 1f;
            var cache = BotCacheUtility.GetCache(bot);
            if (cache?.PanicHandler != null)
                composure = Mathf.Clamp01(cache.PanicHandler.GetComposureLevel());

            float radiusBoost = Mathf.Lerp(1.0f, 1.3f, profile?.RiskTolerance ?? 0.5f);
            float effectiveRetreat = RetreatDistance * radiusBoost;

            Dictionary<Vector3, float> candidates = new Dictionary<Vector3, float>();

            for (int i = 0; i < MaxSamples; i++)
            {
                float angle = i * (360f / MaxSamples);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
                Vector3 probe = origin + dir * effectiveRetreat;

                if (NavMesh.SamplePosition(probe, out var hit, NavSampleRadius, NavMesh.AllAreas))
                {
                    Vector3 pos = hit.position;

                    bool isFarEnough = true;
                    foreach (Vector3 existing in candidates.Keys)
                    {
                        if ((pos - existing).sqrMagnitude < MinSpacing * MinSpacing)
                        {
                            isFarEnough = false;
                            break;
                        }
                    }

                    if (!isFarEnough)
                        continue;

                    float dangerPenalty = GetDangerPenalty(map, pos);
                    float sneakBonus = (profile != null && profile.IsSilentHunter) ? 0.75f : 1f;
                    float coverScore = Mathf.Max(CoverScorer.ScoreCoverPoint(bot, pos, threatDir), 0.5f);
                    float dist = Vector3.Distance(origin, pos);

                    float score = (dist / coverScore) * dangerPenalty * sneakBonus * (1f + (1f - composure));
                    candidates[pos] = score;
                }
            }

            if (candidates.Count == 0)
                return BuildSafePath(origin, origin + away * RetreatDistance);

            Vector3 best = GetLowestScore(candidates);
            List<Vector3> path = BuildSafePath(origin, best);

            if (path.Count >= 2)
                squadCache[squadId] = path;

            return path;
        }

        #endregion

        #region NavMesh & Scoring Internals

        /// <summary>
        /// Attempts to build a safe path between two points using NavMesh.
        /// </summary>
        private static List<Vector3> BuildSafePath(Vector3 origin, Vector3 target)
        {
            var navPath = new NavMeshPath();
            if (NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, navPath) &&
                navPath.status == NavMeshPathStatus.PathComplete)
            {
                return new List<Vector3>(navPath.corners);
            }

            return new List<Vector3> { origin, target };
        }

        /// <summary>
        /// Ensures that a NavMeshSurface exists for a given map, or builds one if missing.
        /// </summary>
        private static void EnsureNavMeshAvailable(string mapId)
        {
            if (MapNavSurfaces.TryGetValue(mapId, out var surface) && surface != null)
                return;

            GameObject surfaceObj = GameObject.Find("NavMeshSurface") ?? new GameObject("NavMeshSurface");
            surface = surfaceObj.GetComponent<NavMeshSurface>() ?? surfaceObj.AddComponent<NavMeshSurface>();

            surface.collectObjects = CollectObjects.All;
            surface.layerMask = ~0;

            surface.BuildNavMesh();
            MapNavSurfaces[mapId] = surface;
        }

        /// <summary>
        /// Applies penalty if a position lies within a known danger zone.
        /// </summary>
        private static float GetDangerPenalty(string mapId, Vector3 pos)
        {
            List<BotMemoryStore.DangerZone> zones = BotMemoryStore.GetZonesForMap(mapId);
            for (int i = 0; i < zones.Count; i++)
            {
                if (Vector3.Distance(zones[i].Position, pos) < zones[i].Radius)
                    return DangerZonePenalty;
            }

            return 1f;
        }

        /// <summary>
        /// Returns the position from the candidate list with the lowest score.
        /// </summary>
        private static Vector3 GetLowestScore(Dictionary<Vector3, float> dict)
        {
            float min = float.MaxValue;
            Vector3 best = Vector3.zero;

            foreach (var kvp in dict)
            {
                if (kvp.Value < min)
                {
                    min = kvp.Value;
                    best = kvp.Key;
                }
            }

            return best;
        }

        /// <summary>
        /// Clears old retreat paths to avoid stale caching over long matches.
        /// </summary>
        private static void ClearExpiredCache()
        {
            if (Time.time - _lastClearTime > MemoryClearInterval)
            {
                SquadRetreatCache.Clear();
                _lastClearTime = Time.time;
            }
        }

        /// <summary>
        /// Determines whether the given bot is AI-controlled and not the local player.
        /// </summary>
        private static bool IsAIBot(BotOwner bot)
        {
            Player? p = bot.GetPlayer;
            return p != null && p.IsAI && !p.IsYourPlayer;
        }

        #endregion
    }
}
