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
    /// Evaluates nearby cover using NavMesh sampling and danger zone avoidance.
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
        /// Generates a fallback path away from the threat direction using NavMesh + squad-level caching.
        /// </summary>
        /// <param name="bot">Bot instance under threat.</param>
        /// <param name="threatDir">Direction of incoming threat.</param>
        /// <param name="pathCache">Bot-local pathing cache.</param>
        /// <returns>List of Vector3 points forming a retreat path.</returns>
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

            List<Vector3> candidates = new();

            for (int i = 0; i < MaxSamples; i++)
            {
                float angle = i * (360f / MaxSamples);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
                Vector3 probe = origin + dir.normalized * RetreatDistance;

                if (NavMesh.SamplePosition(probe, out NavMeshHit hit, NavSampleRadius, NavMesh.AllAreas))
                {
                    float penalty = GetDangerPenalty(map, hit.position);
                    float sneakBonus = profile?.IsSilentHunter == true ? 0.75f : 1f;
                    float score = Vector3.Distance(origin, hit.position) * penalty * sneakBonus;

                    // Use Y-component as a scoring proxy for sorting
                    candidates.Add(hit.position + Vector3.up * (score * 0.01f));
                }
            }

            if (candidates.Count == 0)
                return new List<Vector3> { origin + away * RetreatDistance };

            candidates.Sort((a, b) => a.y.CompareTo(b.y)); // Lower = better
            Vector3 best = candidates[0];

            List<Vector3> path = BuildNavPath(origin, new Vector3(best.x, origin.y, best.z));

            if (path.Count >= 2)
                squadCache[squadId] = path;

            return path;
        }

        #endregion

        #region Internal Utilities

        /// <summary>
        /// Calculates a nav path between origin and target. Fallbacks to straight line if no path found.
        /// </summary>
        private static List<Vector3> BuildNavPath(Vector3 origin, Vector3 target)
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
        /// Ensures a NavMeshSurface is created for the given map, and builds the navmesh if not yet built.
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
        /// Returns a penalty multiplier if the candidate position lies within any active danger zones.
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
        /// Returns true if the bot is a valid AI entity and not controlled by a player.
        /// </summary>
        private static bool IsAIBot(BotOwner bot)
        {
            var p = bot.GetPlayer;
            return p != null && p.IsAI && !p.IsYourPlayer;
        }

        /// <summary>
        /// Clears all cached retreat paths after a configured interval.
        /// </summary>
        private static void ClearExpiredCache()
        {
            if (Time.time - _lastClearTime > MemoryClearInterval)
            {
                SquadRetreatCache.Clear();
                _lastClearTime = Time.time;
            }
        }

        #endregion
    }
}
