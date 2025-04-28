#nullable enable

namespace AIRefactored.AI.Optimization
{
    using System.Collections.Generic;

    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Memory;
    using AIRefactored.AI.Navigation;
    using AIRefactored.Core;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using EFT;

    using Unity.AI.Navigation;

    using UnityEngine;
    using UnityEngine.AI;

    public static class BotCoverRetreatPlanner
    {
        private const float ChaosOffsetRadius = 2.5f;

        private const float DangerZonePenalty = 0.6f;

        private const int MaxSamples = 10;

        private const float MemoryClearInterval = 60f;

        private const float MinSpacing = 3f;

        private const float NavSampleRadius = 2f;

        private const float RetreatDistance = 12f;

        private const float SquadSpacingThreshold = 4.25f;

        private static readonly List<BotMemoryStore.DangerZone> _zoneBuffer = new(32);

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static readonly Dictionary<string, NavMeshSurface> MapNavSurfaces = new();

        private static readonly Dictionary<string, Dictionary<string, List<Vector3>>> SquadRetreatCache = new();

        private static float _lastClearTime = -99f;

        public static List<Vector3> GetCoverRetreatPath(
            BotOwner bot,
            Vector3 threatDir,
            BotOwnerPathfindingCache pathCache)
        {
            if (!IsAIBot(bot) || bot.Transform == null)
                return new List<Vector3>();

            ClearExpiredCache();

            string map = GameWorldHandler.GetCurrentMapName();
            string squadId = bot.Profile?.Info?.GroupId ?? bot.ProfileId;

            if (!SquadRetreatCache.TryGetValue(map, out var squadCache))
                SquadRetreatCache[map] = squadCache = new();

            if (squadCache.TryGetValue(squadId, out var cachedPath))
            {
                if (IsPathBlockedByDoor(cachedPath) || IsPathUnsafe(bot, cachedPath))
                    squadCache.Remove(squadId);
                else if (cachedPath.Count >= 2)
                    return cachedPath;
            }

            var surface = GetNavMeshSurfaceForMap(map);
            if (surface == null)
                return new List<Vector3>();

            var profile = BotRegistry.Get(bot.ProfileId);
            Vector3 origin = bot.Position;
            Vector3 away = -threatDir.normalized;

            float composure = BotCacheUtility.GetCache(bot)?.PanicHandler?.GetComposureLevel() ?? 1f;
            float effectiveRetreat = RetreatDistance * Mathf.Lerp(1.0f, 1.3f, profile?.RiskTolerance ?? 0.5f);

            Dictionary<Vector3, float> scoredCandidates = new();

            // Sample NavMesh around bot dynamically
            for (int i = 0; i < MaxSamples; i++)
            {
                float angle = i * (360f / MaxSamples);
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * away;
                Vector3 probe = origin + dir * effectiveRetreat;

                if (!NavMesh.SamplePosition(probe, out var hit, NavSampleRadius, NavMesh.AllAreas))
                    continue;

                Vector3 pos = hit.position;
                if (!IsFarEnough(pos, scoredCandidates.Keys) || HasSquadConflict(bot, pos)
                                                             || IsPositionUnsafe(bot, pos))
                    continue;

                float score = ScoreFallbackPoint(bot, pos, threatDir, profile, composure);
                scoredCandidates[pos] = score;
            }

            // Sample tactical NavPoints properly
            var tacticalPoints = NavPointRegistry.QueryNearby(
                origin,
                25f,
                (NavPointData p) =>
                    {
                        if (Vector3.Dot((p.Position - origin).normalized, away) < 0.4f)
                            return false;

                        if (ZoneAssignmentHelper.GetNearestZone(origin) == p.Zone)
                            return false;

                        return true;
                    });

            foreach (var point in tacticalPoints)
            {
                if (!IsFarEnough(point.Position, scoredCandidates.Keys))
                    continue;

                float score = ScoreNavPoint(bot, point, threatDir, profile, composure);
                scoredCandidates[point.Position] = score;
            }

            if (scoredCandidates.Count == 0)
                return BuildFallbackPath(
                    origin,
                    origin + away * RetreatDistance + Random.insideUnitSphere * ChaosOffsetRadius);

            Vector3 best = GetLowestScore(scoredCandidates);
            List<Vector3> path = pathCache.GetOptimizedPath(bot, best);

            if (path.Count >= 2)
                squadCache[squadId] = path;

            return path;
        }

        public static void RegisterSurface(string mapName, NavMeshSurface surface)
        {
            if (!string.IsNullOrEmpty(mapName) && surface != null)
                MapNavSurfaces[mapName] = surface;
        }

        private static List<Vector3> BuildFallbackPath(Vector3 origin, Vector3 target)
        {
            var navPath = new NavMeshPath();
            return NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, navPath)
                   && navPath.status == NavMeshPathStatus.PathComplete
                       ? new List<Vector3>(navPath.corners)
                       : new List<Vector3> { origin, target };
        }

        private static void ClearExpiredCache()
        {
            if (Time.time - _lastClearTime > MemoryClearInterval)
            {
                SquadRetreatCache.Clear();
                _lastClearTime = Time.time;
            }
        }

        private static float GetDangerPenalty(string mapId, Vector3 pos)
        {
            _zoneBuffer.Clear();
            _zoneBuffer.AddRange(BotMemoryStore.GetZonesForMap(mapId));

            foreach (var zone in _zoneBuffer)
                if (Vector3.Distance(zone.Position, pos) < zone.Radius)
                    return DangerZonePenalty;

            return 1f;
        }

        private static Vector3 GetLowestScore(Dictionary<Vector3, float> candidates)
        {
            float min = float.MaxValue;
            Vector3 best = Vector3.zero;
            foreach (var kvp in candidates)
                if (kvp.Value < min)
                {
                    min = kvp.Value;
                    best = kvp.Key;
                }

            return best;
        }

        private static NavMeshSurface? GetNavMeshSurfaceForMap(string map)
        {
            MapNavSurfaces.TryGetValue(map, out var surface);
            return surface;
        }

        private static bool HasSquadConflict(BotOwner bot, Vector3 pos)
        {
            var group = bot.BotsGroup;
            if (group == null) return false;

            for (int i = 0; i < group.MembersCount; i++)
            {
                var mate = group.Member(i);
                if (mate != null && mate != bot && !mate.IsDead
                    && Vector3.Distance(mate.Position, pos) < SquadSpacingThreshold)
                    return true;
            }

            return false;
        }

        private static bool IsAIBot(BotOwner bot)
        {
            var player = bot.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        private static bool IsFarEnough(Vector3 pos, IEnumerable<Vector3> existing)
        {
            foreach (var point in existing)
                if ((pos - point).sqrMagnitude < MinSpacing * MinSpacing)
                    return false;
            return true;
        }

        private static bool IsPathBlockedByDoor(List<Vector3> path)
        {
            if (path.Count < 2) return false;
            Vector3 origin = path[0] + Vector3.up * 1.2f;
            Vector3 dir = (path[1] - path[0]).normalized;
            return Physics.Raycast(
                origin,
                dir,
                out _,
                Vector3.Distance(path[0], path[1]) + 0.5f,
                LayerMaskClass.DoorLayer);
        }

        private static bool IsPathUnsafe(BotOwner bot, List<Vector3> path)
        {
            var memory = BotCacheUtility.GetCache(bot)?.TacticalMemory;
            if (memory == null) return false;

            foreach (var point in path)
                if (memory.IsZoneUnsafe(point))
                    return true;
            return false;
        }

        private static bool IsPositionUnsafe(BotOwner bot, Vector3 pos)
        {
            return BotCacheUtility.GetCache(bot)?.TacticalMemory?.IsZoneUnsafe(pos) == true;
        }

        private static float ScoreFallbackPoint(
            BotOwner bot,
            Vector3 pos,
            Vector3 threatDir,
            BotPersonalityProfile? profile,
            float composure)
        {
            float coverScore = Mathf.Max(CoverScorer.ScoreCoverPoint(bot, pos, threatDir), 0.5f);
            float dist = Vector3.Distance(bot.Position, pos);
            float dangerPenalty = GetDangerPenalty(GameWorldHandler.GetCurrentMapName(), pos);
            float sneakFactor = (profile?.IsSilentHunter ?? false) ? 0.75f : 1f;

            return (dist / coverScore) * dangerPenalty * sneakFactor * (1f + (1f - composure));
        }

        private static float ScoreNavPoint(
            BotOwner bot,
            NavPointData point,
            Vector3 threatDir,
            BotPersonalityProfile? profile,
            float composure)
        {
            float coverScore = point.IsCover ? 1.0f : 0.75f;
            float dist = Vector3.Distance(bot.Position, point.Position);
            float dangerPenalty = GetDangerPenalty(GameWorldHandler.GetCurrentMapName(), point.Position);
            float sneakFactor = (profile?.IsSilentHunter ?? false) ? 0.75f : 1f;

            float elevationBonus = point.ElevationBand == "High" ? 0.85f : point.ElevationBand == "Mid" ? 0.95f : 1.0f;

            float zoneBias = point.IsIndoor ? 0.9f : 1.0f;

            return (dist / coverScore) * dangerPenalty * sneakFactor * elevationBonus * zoneBias
                   * (1f + (1f - composure));
        }
    }
}