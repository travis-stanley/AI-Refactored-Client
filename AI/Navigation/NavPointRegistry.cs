#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Navigation
{
    /// <summary>
    /// Central registry for tactical navigation points with metadata.
    /// Supports cover tagging, elevation, indoor/outdoor classification, zone tags,
    /// jumpability flags, cover peek orientation, and fast quadtree spatial indexing.
    /// </summary>
    public static class NavPointRegistry
    {
        #region Internal Types

        private class NavPoint
        {
            public Vector3 WorldPos { get; }
            public bool IsCover { get; }
            public string Tag { get; }
            public float Elevation { get; }
            public bool IsIndoor { get; }
            public bool IsJumpable { get; }
            public float CoverAngle { get; }

            public NavPoint(Vector3 pos, bool isCover, string tag, float elevation, bool isIndoor, bool isJumpable, float coverAngle)
            {
                WorldPos = pos;
                IsCover = isCover;
                Tag = tag;
                Elevation = elevation;
                IsIndoor = isIndoor;
                IsJumpable = isJumpable;
                CoverAngle = coverAngle;
            }
        }

        #endregion

        #region Fields

        private static readonly List<NavPoint> _points = new(512);
        private static readonly HashSet<Vector3> _unique = new();
        private static QuadtreeNavGrid? _quadtree;
        private static bool _useQuadtree;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Public API

        public static int Count => _points.Count;

        public static void EnableSpatialIndexing(bool enable)
        {
            _useQuadtree = enable;
            if (enable) InitializeSpatialIndex(); else _quadtree = null;
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

            _points.Add(new NavPoint(pos, isCover, tag, elevation, isIndoor, isJumpable, coverAngle));
            _unique.Add(pos);

            if (_useQuadtree)
                _quadtree?.Insert(pos);
        }

        public static void Clear()
        {
            _points.Clear();
            _unique.Clear();
            _quadtree = null;
        }

        public static void InitializeSpatialIndex()
        {
            if (!_useQuadtree || _points.Count == 0)
                return;

            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            foreach (var point in _points)
            {
                Vector3 pos = point.WorldPos;
                minX = Mathf.Min(minX, pos.x);
                maxX = Mathf.Max(maxX, pos.x);
                minZ = Mathf.Min(minZ, pos.z);
                maxZ = Mathf.Max(maxZ, pos.z);
            }

            float padding = 10f;
            Vector2 center = new((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
            float size = Mathf.Max(maxX - minX, maxZ - minZ) + padding * 2f;

            _quadtree = new QuadtreeNavGrid(center, size);
            foreach (var point in _points)
                _quadtree.Insert(point.WorldPos);

            Logger.LogInfo($"[NavPointRegistry] 🧭 Built quadtree for {Count} nav points. Size: {size:F1} at {center}");
        }

        public static List<Vector3> GetAllPositions()
        {
            List<Vector3> result = new(_points.Count);
            foreach (var p in _points)
                result.Add(p.WorldPos);
            return result;
        }

        public static bool IsCoverPoint(Vector3 pos)
        {
            for (int i = 0; i < _points.Count; i++)
                if (_points[i].WorldPos == pos)
                    return _points[i].IsCover;
            return false;
        }

        public static bool IsJumpable(Vector3 pos)
        {
            for (int i = 0; i < _points.Count; i++)
                if (_points[i].WorldPos == pos)
                    return _points[i].IsJumpable;
            return false;
        }

        public static float GetCoverAngle(Vector3 pos)
        {
            for (int i = 0; i < _points.Count; i++)
                if (_points[i].WorldPos == pos)
                    return _points[i].CoverAngle;
            return 0f;
        }

        public static bool IsIndoor(Vector3 pos)
        {
            for (int i = 0; i < _points.Count; i++)
                if (_points[i].WorldPos == pos)
                    return _points[i].IsIndoor;
            return false;
        }

        public static string? GetTag(Vector3 pos)
        {
            for (int i = 0; i < _points.Count; i++)
                if (_points[i].WorldPos == pos)
                    return _points[i].Tag;
            return null;
        }

        public static float GetElevation(Vector3 pos)
        {
            for (int i = 0; i < _points.Count; i++)
                if (_points[i].WorldPos == pos)
                    return _points[i].Elevation;
            return 0f;
        }

        public static bool IsRegistered(Vector3 pos) => _unique.Contains(pos);

        public static List<Vector3> QueryNearby(Vector3 origin, float radius, Predicate<Vector3>? filter = null, bool coverOnly = false)
        {
            List<Vector3> result = new(16);
            float radiusSq = radius * radius;

            if (_useQuadtree && _quadtree != null)
            {
                var raw = _quadtree.QueryRaw(origin, radius, filter);
                foreach (var pos in raw)
                {
                    for (int i = 0; i < _points.Count; i++)
                    {
                        var nav = _points[i];
                        if (nav.WorldPos == pos && (!coverOnly || nav.IsCover))
                        {
                            result.Add(nav.WorldPos);
                            break;
                        }
                    }
                }

                return result;
            }

            for (int i = 0; i < _points.Count; i++)
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

        #endregion
    }
}
