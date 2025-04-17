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
                Vector3 probe = origin + dir * RetreatDistance;

                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, NavSampleRadius, NavMesh.AllAreas))
                {
                    float dangerPenalty = GetDangerPenalty(map, hit.position);
                    float sneakBonus = profile?.IsSilentHunter == true ? 0.75f : 1f;
                    float coverScore = CoverScorer.ScoreCoverPoint(bot, hit.position, threatDir);

                    float score = (Vector3.Distance(origin, hit.position) / coverScore) * dangerPenalty * sneakBonus;

                    if (!candidates.ContainsKey(hit.position))
                        candidates[hit.position] = score;
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

        #region Internal Utilities

        /// <summary>
        /// Builds a NavMesh path between two points. Returns origin → target if NavMesh fails.
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
        /// Ensures the map's NavMesh is built and cached.
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
        /// Applies suppression penalty for points inside any active danger zone.
        /// </summary>
        private static float GetDangerPenalty(string mapId, Vector3 pos)
        {
            var zones = BotMemoryStore.GetZonesForMap(mapId);
            for (int i = 0; i < zones.Count; i++)
            {
                if (Vector3.Distance(zones[i].Position, pos) < zones[i].Radius)
                    return DangerZonePenalty;
            }
            return 1f;
        }

        /// <summary>
        /// Determines whether a bot is an AI, not a human or coop client.
        /// </summary>
        private static bool IsAIBot(BotOwner bot)
        {
            var p = bot.GetPlayer;
            return p != null && p.IsAI && !p.IsYourPlayer;
        }

        /// <summary>
        /// Periodically clears stale pathing cache for squads.
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
        /// Gets the lowest scoring cover point candidate from a dictionary.
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

        #endregion
    }
}
