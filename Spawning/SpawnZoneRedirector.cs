#nullable enable

using System.Collections.Generic;
using EFT;
using EFT.Game.Spawning;
using UnityEngine;

namespace AIRefactored.Spawning
{
    public static class SpawnZoneRedirector
    {
        private const float MIN_CLEAR_RADIUS = 1.5f;
        private const float MAX_OCCLUSION_DISTANCE = 20f;

        private static readonly LayerMask OcclusionMask = LayerMask.GetMask("Default", "Environment", "Terrain");

        private static readonly Dictionary<string, List<ISpawnPoint>> _groupAssignments = new();
        private static readonly HashSet<string> _occupiedSpawnIds = new();

        public static void ReserveGroupSpawn(string groupId, int count, bool isPmc)
        {
            if (string.IsNullOrEmpty(groupId) || _groupAssignments.ContainsKey(groupId))
                return;

            var all = SpawnPointRegistry.GetAll();
            List<ISpawnPoint> reserved = new();

            foreach (var point in all)
            {
                if (reserved.Count >= count)
                    break;

                if (IsValid(point, isPmc) && !_occupiedSpawnIds.Contains(point.Id))
                {
                    reserved.Add(point);
                    _occupiedSpawnIds.Add(point.Id);
                }
            }

            _groupAssignments[groupId] = reserved;
        }


        public static ISpawnPoint? GetRedirectedSpawn(string groupId, bool isPmc)
        {
            // Try reserved group spawns first
            if (!string.IsNullOrEmpty(groupId) && _groupAssignments.TryGetValue(groupId, out var list))
            {
                foreach (var point in list)
                {
                    if (IsValid(point, isPmc))
                        return point;
                }
            }

            // Fallback to general pool
            return GetRandomAvailableSpawn(groupId, isPmc);
        }

        public static ISpawnPoint? GetRandomAvailableSpawn(string? groupId = null, bool isPmc = false)
        {
            var allPoints = SpawnPointRegistry.GetAll();
            List<ISpawnPoint> valid = new();

            foreach (var point in allPoints)
            {
                if (!_occupiedSpawnIds.Contains(point.Id) && IsValid(point, isPmc))
                    valid.Add(point);
            }

            if (valid.Count == 0)
                return null;

            var chosen = valid[UnityEngine.Random.Range(0, valid.Count)];
            _occupiedSpawnIds.Add(chosen.Id);
            return chosen;
        }


        public static bool IsValid(ISpawnPoint point) => IsValid(point, false);

        public static bool IsValid(ISpawnPoint point, bool requirePmc)
        {
            Vector3 pos = point.Position;

            if (Physics.OverlapSphere(pos, MIN_CLEAR_RADIUS).Length > 0)
                return false;

            if (!HasGroundSupport(pos))
                return false;

            // PMC bots must spawn in zones marked for Player category
            if (requirePmc && !point.Categories.HasFlag(ESpawnCategoryMask.Player))
                return false;

            return true;
        }

        private static bool HasGroundSupport(Vector3 origin)
        {
            Vector3 rayStart = origin + Vector3.up * 1f;
            return Physics.Raycast(rayStart, Vector3.down, MAX_OCCLUSION_DISTANCE, OcclusionMask);
        }

        public static void Reset()
        {
            _occupiedSpawnIds.Clear();
            _groupAssignments.Clear();
        }

#if UNITY_EDITOR
        public static void LogSpawnAttempt(string label, Vector3 pos)
        {
            Debug.Log($"[SpawnRedirector] {label} redirected to {pos}");
        }
#endif
    }
}
