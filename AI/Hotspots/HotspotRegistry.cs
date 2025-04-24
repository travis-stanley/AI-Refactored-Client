#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    /// <summary>
    /// Global registry for map-specific hotspots (loot zones, patrol targets, tactical nodes).
    /// Dynamically builds spatial indexes (quadtree or grid) depending on map type.
    /// </summary>
    public static class HotspotRegistry
    {
        #region Types

        public class Hotspot
        {
            public Vector3 Position { get; }
            public string Zone { get; }

            public Hotspot(Vector3 pos, string zone)
            {
                Position = pos;
                Zone = zone;
            }
        }

        private enum SpatialIndexMode { None, Quadtree, Grid }

        #endregion

        #region Internal Fields

        private static readonly List<Hotspot> _all = new(256);
        private static readonly Dictionary<string, List<Hotspot>> _byZone = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, SpatialIndexMode> _indexModeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["woods"] = SpatialIndexMode.Quadtree,
            ["shoreline"] = SpatialIndexMode.Quadtree,
            ["lighthouse"] = SpatialIndexMode.Quadtree,
            ["interchange"] = SpatialIndexMode.Quadtree,
            ["bigmap"] = SpatialIndexMode.Quadtree,

            ["sandbox"] = SpatialIndexMode.Grid,
            ["sandbox_high"] = SpatialIndexMode.Grid,
            ["factory4_day"] = SpatialIndexMode.Grid,
            ["factory4_night"] = SpatialIndexMode.Grid,
            ["laboratory"] = SpatialIndexMode.Grid,
            ["tarkovstreets"] = SpatialIndexMode.Grid,
            ["rezervbase"] = SpatialIndexMode.Grid,
        };

        private static SpatialIndexMode _activeMode = SpatialIndexMode.None;
        private static HotspotQuadtree? _quadtree;
        private static HotspotSpatialGrid? _grid;
        private static string _loadedMap = "none";

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Initializes the registry for a new map, clearing previous hotspots and rebuilding indexes.
        /// </summary>
        public static void Initialize(string mapId)
        {
            if (string.IsNullOrEmpty(mapId) || _loadedMap.Equals(mapId, StringComparison.OrdinalIgnoreCase))
                return;

            Clear();

            _loadedMap = mapId.ToLowerInvariant();
            _activeMode = _indexModeMap.TryGetValue(_loadedMap, out var mode) ? mode : SpatialIndexMode.None;

            var set = HardcodedHotspots.GetForMap(_loadedMap);
            if (set == null || set.Points.Count == 0)
            {
                _log.LogWarning($"[HotspotRegistry] ⚠ No hotspots found for map '{_loadedMap}'");
                return;
            }

            foreach (var entry in set.Points)
            {
                var hotspot = new Hotspot(entry.Position, entry.Zone);
                _all.Add(hotspot);

                if (!_byZone.TryGetValue(entry.Zone, out var list))
                {
                    list = new List<Hotspot>(8);
                    _byZone[entry.Zone] = list;
                }

                list.Add(hotspot);
            }

            switch (_activeMode)
            {
                case SpatialIndexMode.Quadtree:
                    BuildQuadtree();
                    break;
                case SpatialIndexMode.Grid:
                    BuildGrid();
                    break;
            }

            _log.LogInfo($"[HotspotRegistry] ✅ Registered {_all.Count} hotspots for map '{_loadedMap}' using {_activeMode}");
        }

        public static void Clear()
        {
            _all.Clear();
            _byZone.Clear();
            _quadtree = null;
            _grid = null;
            _loadedMap = "none";
            _activeMode = SpatialIndexMode.None;
        }

        public static IReadOnlyList<Hotspot> GetAll() => _all;

        public static IReadOnlyList<Hotspot> GetAllInZone(string zone)
        {
            return _byZone.TryGetValue(zone, out var list) ? list : Array.Empty<Hotspot>();
        }

        public static List<Hotspot> QueryNearby(Vector3 position, float radius, Predicate<Hotspot>? filter = null)
        {
            switch (_activeMode)
            {
                case SpatialIndexMode.Quadtree when _quadtree != null:
                    return _quadtree.Query(position, radius, filter);
                case SpatialIndexMode.Grid when _grid != null:
                    return _grid.Query(position, radius, filter);
                default:
                    return FallbackQuery(position, radius, filter);
            }
        }

        public static Hotspot GetRandomHotspot()
        {
            return _all.Count == 0
                ? new Hotspot(Vector3.zero, "none")
                : _all[UnityEngine.Random.Range(0, _all.Count)];
        }

        #endregion

        #region Spatial Index Construction

        private static void BuildQuadtree()
        {
            Vector2 center = EstimateCenter();
            float size = EstimateBoundsSize(center);
            _quadtree = new HotspotQuadtree(center, size);

            for (int i = 0; i < _all.Count; i++)
                _quadtree.Insert(_all[i]);
        }

        private static void BuildGrid()
        {
            _grid = new HotspotSpatialGrid(cellSize: 10f);
            for (int i = 0; i < _all.Count; i++)
                _grid.Insert(_all[i]);
        }

        private static List<Hotspot> FallbackQuery(Vector3 pos, float radius, Predicate<Hotspot>? filter)
        {
            List<Hotspot> results = new(16);
            float radiusSq = radius * radius;

            for (int i = 0; i < _all.Count; i++)
            {
                Hotspot h = _all[i];
                if ((h.Position - pos).sqrMagnitude <= radiusSq &&
                    (filter == null || filter(h)))
                {
                    results.Add(h);
                }
            }

            return results;
        }

        private static Vector2 EstimateCenter()
        {
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < _all.Count; i++)
            {
                Vector3 p = _all[i].Position;
                if (p.x < minX) minX = p.x;
                if (p.x > maxX) maxX = p.x;
                if (p.z < minZ) minZ = p.z;
                if (p.z > maxZ) maxZ = p.z;
            }

            return new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        }

        private static float EstimateBoundsSize(Vector2 center)
        {
            float maxDist = 0f;
            for (int i = 0; i < _all.Count; i++)
            {
                Vector3 p = _all[i].Position;
                float dist = Vector2.Distance(new Vector2(p.x, p.z), center);
                if (dist > maxDist)
                    maxDist = dist;
            }

            return Mathf.NextPowerOfTwo(Mathf.CeilToInt(maxDist * 2f));
        }

        #endregion
    }
}
