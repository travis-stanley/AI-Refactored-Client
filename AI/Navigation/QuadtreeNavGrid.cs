#nullable enable

namespace AIRefactored.AI.Navigation
{
    using System;
    using System.Collections.Generic;

    using UnityEngine;

    /// <summary>
    ///     Quadtree-based spatial index for NavPointData.
    ///     Supports fast lookup with optional filtering by zone, elevation, and cover tag.
    /// </summary>
    public class QuadtreeNavGrid
    {
        private const int MaxDepth = 6;

        private const int MaxPointsPerNode = 8;

        private Node _root;

        public QuadtreeNavGrid(Vector2 center, float size)
        {
            var half = size * 0.5f;
            this._root = new Node(new Rect(center.x - half, center.y - half, size, size), 0);
        }

        public void Clear()
        {
            this._root = new Node(this._root.Bounds, 0);
        }

        public void Insert(NavPointData point)
        {
            if (point == null) return;
            this.Insert(this._root, point);
        }

        public void Insert(Vector3 point)
        {
            this.Insert(this._root, point);
        }

        public List<NavPointData> Query(Vector3 position, float radius, Predicate<NavPointData>? filter = null)
        {
            var result = new List<NavPointData>();
            this.Query(this._root, position, radius * radius, result, filter);
            return result;
        }

        /// <summary>
        ///     Performs compound queries against zone, elevation band, and cover tag.
        /// </summary>
        public List<NavPointData> QueryCombined(
            Vector3 position,
            float radius,
            string? zone = null,
            string? elevationBand = null,
            string? coverTag = null)
        {
            Predicate<NavPointData> filter = (NavPointData p) =>
                {
                    if (zone != null && !string.Equals(p.Zone, zone, StringComparison.OrdinalIgnoreCase))
                        return false;

                    if (elevationBand != null && !string.Equals(
                            p.ElevationBand,
                            elevationBand,
                            StringComparison.OrdinalIgnoreCase))
                        return false;

                    if (coverTag != null && !string.Equals(p.Tag, coverTag, StringComparison.OrdinalIgnoreCase))
                        return false;

                    return true;
                };

            return this.Query(position, radius, filter);
        }

        public List<Vector3> QueryRaw(Vector3 position, float radius, Predicate<Vector3>? filter = null)
        {
            var result = new List<Vector3>();
            this.QueryRaw(this._root, position, radius * radius, result, filter);
            return result;
        }

        private void Insert(Node node, NavPointData point)
        {
            var pos2D = new Vector2(point.Position.x, point.Position.z);
            if (!node.Bounds.Contains(pos2D)) return;

            if (node.IsLeaf)
            {
                node.NavPoints.Add(point);

                if (node.NavPoints.Count > MaxPointsPerNode && node.Depth < MaxDepth)
                {
                    this.Subdivide(node);
                    this.ReinsertPoints(node);
                }
            }
            else
            {
                foreach (var child in node.Children!) this.Insert(child, point);
            }
        }

        private void Insert(Node node, Vector3 point)
        {
            var pos2D = new Vector2(point.x, point.z);
            if (!node.Bounds.Contains(pos2D)) return;

            if (node.IsLeaf)
            {
                node.RawPoints.Add(point);

                if (node.RawPoints.Count > MaxPointsPerNode && node.Depth < MaxDepth)
                {
                    this.Subdivide(node);
                    this.ReinsertPoints(node);
                }
            }
            else
            {
                foreach (var child in node.Children!) this.Insert(child, point);
            }
        }

        private void Query(
            Node node,
            Vector3 worldPos,
            float radiusSq,
            List<NavPointData> result,
            Predicate<NavPointData>? filter)
        {
            var pos2D = new Vector2(worldPos.x, worldPos.z);
            var radius = Mathf.Sqrt(radiusSq);
            var queryRect = new Rect(pos2D.x - radius, pos2D.y - radius, radius * 2f, radius * 2f);

            if (!node.Bounds.Overlaps(queryRect))
                return;

            if (node.IsLeaf)
                foreach (var point in node.NavPoints)
                {
                    var distSq = (point.Position - worldPos).sqrMagnitude;
                    if (distSq <= radiusSq && (filter == null || filter(point)))
                        result.Add(point);
                }
            else
                foreach (var child in node.Children!)
                    this.Query(child, worldPos, radiusSq, result, filter);
        }

        private void QueryRaw(
            Node node,
            Vector3 worldPos,
            float radiusSq,
            List<Vector3> result,
            Predicate<Vector3>? filter)
        {
            var pos2D = new Vector2(worldPos.x, worldPos.z);
            var radius = Mathf.Sqrt(radiusSq);
            var queryRect = new Rect(pos2D.x - radius, pos2D.y - radius, radius * 2f, radius * 2f);

            if (!node.Bounds.Overlaps(queryRect))
                return;

            if (node.IsLeaf)
                foreach (var point in node.RawPoints)
                {
                    var distSq = (point - worldPos).sqrMagnitude;
                    if (distSq <= radiusSq && (filter == null || filter(point)))
                        result.Add(point);
                }
            else
                foreach (var child in node.Children!)
                    this.QueryRaw(child, worldPos, radiusSq, result, filter);
        }

        private void ReinsertPoints(Node node)
        {
            var navCopy = node.NavPoints;
            node.NavPoints = new List<NavPointData>();

            foreach (var point in navCopy)
            foreach (var child in node.Children!)
                this.Insert(child, point);

            var rawCopy = node.RawPoints;
            node.RawPoints = new List<Vector3>();

            foreach (var point in rawCopy)
            foreach (var child in node.Children!)
                this.Insert(child, point);
        }

        private void Subdivide(Node node)
        {
            node.Children = new Node[4];
            var halfW = node.Bounds.width * 0.5f;
            var halfH = node.Bounds.height * 0.5f;

            node.Children[0] = new Node(
                new Rect(node.Bounds.x, node.Bounds.y, halfW, halfH),
                node.Depth + 1); // Bottom Left
            node.Children[1] = new Node(
                new Rect(node.Bounds.x + halfW, node.Bounds.y, halfW, halfH),
                node.Depth + 1); // Bottom Right
            node.Children[2] = new Node(
                new Rect(node.Bounds.x, node.Bounds.y + halfH, halfW, halfH),
                node.Depth + 1); // Top Left
            node.Children[3] = new Node(
                new Rect(node.Bounds.x + halfW, node.Bounds.y + halfH, halfW, halfH),
                node.Depth + 1); // Top Right
        }

        private class Node
        {
            public readonly int Depth;

            public Rect Bounds;

            public Node[]? Children;

            public List<NavPointData> NavPoints;

            public List<Vector3> RawPoints;

            public Node(Rect bounds, int depth)
            {
                this.Bounds = bounds;
                this.NavPoints = new List<NavPointData>(MaxPointsPerNode);
                this.RawPoints = new List<Vector3>(MaxPointsPerNode);
                this.Depth = depth;
            }

            public bool IsLeaf => this.Children == null;
        }
    }
}