#nullable enable

namespace AIRefactored.AI.Navigation
{
    using System;
    using System.Collections.Generic;

    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using UnityEngine;

    /// <summary>
    ///     Central registry for tactical navigation points with metadata.
    ///     Supports cover tagging, elevation bands, indoor/outdoor classification, zone detection,
    ///     jumpability flags, cover orientation, and fast quadtree spatial indexing.
    /// </summary>
    public static class NavPointRegistry
    {
        private static readonly List<NavPoint> _points = new(512);

        private static readonly HashSet<Vector3> _unique = new();

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static QuadtreeNavGrid? _quadtree;

        private static bool _useQuadtree;

        private static IZones? _zones;

        public static int Count => _points.Count;

        public static void Clear()
        {
            _points.Clear();
            _unique.Clear();
            _quadtree = null;
        }

        public static void EnableSpatialIndexing(bool enable)
        {
            _useQuadtree = enable;
            if (enable) InitializeSpatialIndex();
            else _quadtree = null;
        }

        public static List<Vector3> GetAllPositions()
        {
            List<Vector3> result = new(_points.Count);
            foreach (var p in _points)
                result.Add(p.WorldPos);
            return result;
        }

        public static float GetCoverAngle(Vector3 pos)
        {
            return TryGetPoint(pos, out var point) ? point.CoverAngle : 0f;
        }

        public static float GetElevation(Vector3 pos)
        {
            return TryGetPoint(pos, out var point) ? point.Elevation : 0f;
        }

        public static string? GetElevationBand(Vector3 pos)
        {
            return TryGetPoint(pos, out var point) ? point.ElevationBand : null;
        }

        public static string? GetTag(Vector3 pos)
        {
            return TryGetPoint(pos, out var point) ? point.Tag : null;
        }

        public static string? GetZone(Vector3 pos)
        {
            return TryGetPoint(pos, out var point) ? point.Zone : null;
        }

        public static void InitializeSpatialIndex()
        {
            if (!_useQuadtree || _points.Count == 0)
                return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var point in _points)
            {
                var pos = point.WorldPos;
                minX = Mathf.Min(minX, pos.x);
                maxX = Mathf.Max(maxX, pos.x);
                minZ = Mathf.Min(minZ, pos.z);
                maxZ = Mathf.Max(maxZ, pos.z);
            }

            var padding = 10f;
            Vector2 center = new((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            var size = Mathf.Max(maxX - minX, maxZ - minZ) + padding * 2f;

            _quadtree = new QuadtreeNavGrid(center, size);
            foreach (var point in _points)
                _quadtree.Insert(point.WorldPos);

            Logger.LogInfo($"[NavPointRegistry] Built quadtree for {Count} nav points. Size: {size:F1} at {center}");
        }

        public static void InitializeZoneSystem(IZones zones)
        {
            _zones = zones;
        }

        public static bool IsCoverPoint(Vector3 pos)
        {
            return TryGetPoint(pos, out var point) && point.IsCover;
        }

        public static bool IsIndoor(Vector3 pos)
        {
            return TryGetPoint(pos, out var point) && point.IsIndoor;
        }

        public static bool IsJumpable(Vector3 pos)
        {
            return TryGetPoint(pos, out var point) && point.IsJumpable;
        }

        public static bool IsRegistered(Vector3 pos)
        {
            return _unique.Contains(pos);
        }

        public static List<Vector3> QueryNearby(
            Vector3 origin,
            float radius,
            Predicate<Vector3>? filter = null,
            bool coverOnly = false)
        {
            List<Vector3> result = new(16);
            var radiusSq = radius * radius;

            if (_useQuadtree && _quadtree != null)
            {
                var raw = _quadtree.QueryRaw(origin, radius, filter);
                for (var i = 0; i < raw.Count; i++)
                {
                    var pos = raw[i];
                    if (TryGetPoint(pos, out var nav) && (!coverOnly || nav.IsCover))
                        result.Add(pos);
                }

                return result;
            }

            for (var i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                if ((p.WorldPos - origin).sqrMagnitude > radiusSq)
                    continue;

                if (coverOnly && !p.IsCover)
                    continue;

                if (filter == null || filter(p.WorldPos))
                    result.Add(p.WorldPos);
            }

            return result;
        }

        /// <summary>
        ///     Tactical-aware QueryNearby that returns full NavPointData metadata.
        /// </summary>
        public static List<NavPointData> QueryNearby(
            Vector3 origin,
            float radius,
            Predicate<NavPointData>? filter = null)
        {
            List<NavPointData> result = new(16);
            var radiusSq = radius * radius;

            for (var i = 0; i < _points.Count; i++)
            {
                var p = _points[i];
                if ((p.WorldPos - origin).sqrMagnitude > radiusSq)
                    continue;

                var data = new NavPointData(
                    p.WorldPos,
                    p.IsCover,
                    p.Tag,
                    p.Elevation,
                    p.IsIndoor,
                    p.IsJumpable,
                    p.CoverAngle,
                    p.Zone,
                    p.ElevationBand);

                if (filter == null || filter(data))
                    result.Add(data);
            }

            return result;
        }

        public static void Register(
            Vector3 pos,
            bool isCover = false,
            string tag = "generic",
            float elevation = 0f,
            bool isIndoor = false,
            bool isJumpable = false,
            float coverAngle = 0f)
        {
            if (_unique.Contains(pos))
                return;

            var zoneName = GetNearestZone(pos);
            var elevationBand = GetElevationBand(elevation);

            var point = new NavPoint(
                pos,
                isCover,
                tag,
                elevation,
                isIndoor,
                isJumpable,
                coverAngle,
                zoneName,
                elevationBand);
            _points.Add(point);
            _unique.Add(pos);

            if (_useQuadtree)
                _quadtree?.Insert(pos);
        }

        private static string GetElevationBand(float elevation)
        {
            if (elevation < 2f) return "Low";
            if (elevation < 7f) return "Mid";
            return "High";
        }

        private static string GetNearestZone(Vector3 pos)
        {
            if (_zones == null)
                return "Unknown";

            var result = "Unknown";
            var bestDist = float.MaxValue;

            foreach (var zone in _zones.ZoneNames())
            {
                var spawns = _zones.ZoneSpawnPoints(zone);
                for (var i = 0; i < spawns.Length; i++)
                {
                    var dist = Vector3.Distance(pos, spawns[i].Position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        result = zone;
                    }
                }
            }

            return result;
        }

        private static bool TryGetPoint(Vector3 pos, out NavPoint? point)
        {
            for (var i = 0; i < _points.Count; i++)
                if (_points[i].WorldPos == pos)
                {
                    point = _points[i];
                    return true;
                }

            point = null;
            return false;
        }

        private class NavPoint
        {
            public NavPoint(
                Vector3 pos,
                bool isCover,
                string tag,
                float elevation,
                bool isIndoor,
                bool isJumpable,
                float coverAngle,
                string zone,
                string elevationBand)
            {
                this.WorldPos = pos;
                this.IsCover = isCover;
                this.Tag = tag;
                this.Elevation = elevation;
                this.IsIndoor = isIndoor;
                this.IsJumpable = isJumpable;
                this.CoverAngle = coverAngle;
                this.Zone = zone;
                this.ElevationBand = elevationBand;
            }

            public float CoverAngle { get; }

            public float Elevation { get; }

            public string ElevationBand { get; }

            public bool IsCover { get; }

            public bool IsIndoor { get; }

            public bool IsJumpable { get; }

            public string Tag { get; }

            public Vector3 WorldPos { get; }

            public string Zone { get; }
        }
    }
}