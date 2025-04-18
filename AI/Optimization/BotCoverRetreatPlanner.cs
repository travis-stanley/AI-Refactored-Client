#nullable enable

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
        /// <param name="bot">The bot evaluating retreat.</param>
        /// <param name="threatDir">Direction of the incoming threat.</param>
        /// <param name="pathCache">The bot's local pathfinding cache.</param>
        /// <returns>A list of safe points to move through or toward.</returns>
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

            Dictionary<Vector3, float> candidates = new Dictionary<Vector3, float>();

            for (int i = 0; i < MaxSamples; i++)
            {
                float angle = i * (360f / MaxSamples);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
                Vector3 probe = origin + dir * RetreatDistance;

                NavMeshHit hit;
                if (NavMesh.SamplePosition(probe, out hit, NavSampleRadius, NavMesh.AllAreas))
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
                    float coverScore = CoverScorer.ScoreCoverPoint(bot, pos, threatDir);
                    float dist = Vector3.Distance(origin, pos);

                    float score = (dist / Mathf.Max(coverScore, 0.5f)) * dangerPenalty * sneakBonus;
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
        /// Builds a valid NavMesh path between two points or returns fallback path.
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
        /// Ensures a NavMesh surface is available for the given map.
        /// Builds it dynamically if not present.
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
        /// Scores a position based on proximity to stored danger zones.
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
        /// Finds the position with the lowest retreat penalty score.
        /// </summary>
        private static Vector3 GetLowestScore(Dictionary<Vector3, float> dict)
        {
            float min = float.MaxValue;
            Vector3 best = Vector3.zero;

            foreach (KeyValuePair<Vector3, float> kvp in dict)
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
        /// Clears retreat cache for all squads if expiration time has elapsed.
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
        /// Validates whether this is an AI bot.
        /// </summary>
        private static bool IsAIBot(BotOwner bot)
        {
            Player? p = bot.GetPlayer;
            return p != null && p.IsAI && !p.IsYourPlayer;
        }

        #endregion
    }
}
