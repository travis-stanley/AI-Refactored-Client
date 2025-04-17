#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.Core;
using Unity.AI.Navigation;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Computes and caches dynamic retreat paths for bots under threat.
    /// Uses NavMesh sampling, danger zone avoidance, squad-based caching, and cover desirability.
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

        #region Caches

        private static readonly Dictionary<string, NavMeshSurface> MapNavSurfaces = new();
        private static readonly Dictionary<string, Dictionary<string, List<Vector3>>> SquadRetreatCache = new();
        private static float _lastClearTime = -99f;

        #endregion

        #region Public API

        /// <summary>
        /// Computes a safe fallback path for a bot under threat using local NavMesh samples.
        /// Applies squad-level caching and filters danger zones.
        /// </summary>
        /// <param name="bot">The bot requesting fallback routing.</param>
        /// <param name="threatDir">Direction of incoming threat.</param>
        /// <param name="pathCache">Bot-local navigation cache (optional, improves reuse).</param>
        /// <returns>A list of waypoints forming a fallback route. May return origin + direction fallback if no valid NavMesh found.</returns>
        public static List<Vector3> GetCoverRetreatPath(BotOwner bot, Vector3 threatDir, BotOwnerPathfindingCache pathCache)
        {
            if (!IsAIBot(bot) || bot.Transform == null)
                return new List<Vector3>();

            ClearExpiredCache();

            string map = GameWorldHandler.GetCurrentMapName();
            string squadId = bot.Profile.Info.GroupId ?? bot.ProfileId;

            if (!SquadRetreatCache.TryGetValue(map, out var squadCache))
            {
                squadCache = new Dictionary<string, List<Vector3>>();
                SquadRetreatCache[map] = squadCache;
            }

            if (squadCache.TryGetValue(squadId, out var cachedPath) && cachedPath.Count > 0)
                return cachedPath;

            EnsureNavMeshAvailable(map);

            var profile = BotRegistry.Get(bot.ProfileId);
            Vector3 origin = bot.Position;
            Vector3 away = -threatDir.normalized;

            Dictionary<Vector3, float> candidates = new();

            for (int i = 0; i < MaxSamples; i++)
            {
                float angle = i * (360f / MaxSamples);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
                Vector3 probe = origin + dir.normalized * RetreatDistance;

                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, NavSampleRadius, NavMesh.AllAreas))
                {
                    float dangerPenalty = GetDangerPenalty(map, hit.position);
                    float sneakBonus = profile?.IsSilentHunter == true ? 0.75f : 1f;

                    float coverQuality = CoverScorer.ScoreCoverPoint(bot, hit.position, threatDir);
                    float score = (Vector3.Distance(origin, hit.position) / coverQuality) * dangerPenalty * sneakBonus;

                    if (!candidates.ContainsKey(hit.position))
                        candidates[hit.position] = score;
                }
            }


            if (candidates.Count == 0)
                return new List<Vector3> { origin + away * RetreatDistance };

            // Pick best-scoring candidate
            Vector3 best = GetLowestScore(candidates);
            var path = BuildSafePath(origin, best);


            if (path.Count >= 2)
                squadCache[squadId] = path;

            return path;
        }

        #endregion

        #region Internal Utilities

        /// <summary>
        /// Computes a valid NavMesh path from origin to target. Falls back to a direct line.
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
        /// Ensures a NavMeshSurface is built for the current map.
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
        /// Applies a penalty multiplier for positions inside danger zones.
        /// </summary>
        private static float GetDangerPenalty(string mapId, Vector3 pos)
        {
            var zones = BotMemoryStore.GetZonesForMap(mapId);
            foreach (var zone in zones)
            {
                if (Vector3.Distance(zone.Position, pos) < zone.Radius)
                    return DangerZonePenalty;
            }

            return 1f;
        }

        /// <summary>
        /// Returns true if the given bot is an AI (non-player) agent.
        /// </summary>
        private static bool IsAIBot(BotOwner bot)
        {
            var p = bot.GetPlayer;
            return p != null && p.IsAI && !p.IsYourPlayer;
        }

        /// <summary>
        /// Periodically clears old retreat path memory to avoid stale logic.
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
        /// Returns the key in a dictionary with the lowest float score.
        /// </summary>
        private static Vector3 GetLowestScore(Dictionary<Vector3, float> dict)
        {
            float minScore = float.MaxValue;
            Vector3 best = Vector3.zero;

            foreach (var kvp in dict)
            {
                if (kvp.Value < minScore)
                {
                    minScore = kvp.Value;
                    best = kvp.Key;
                }
            }

            return best;
        }

        #endregion
    }
}
