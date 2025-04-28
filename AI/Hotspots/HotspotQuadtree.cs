#nullable enable

namespace AIRefactored.AI.Hotspots
{
    using System;
    using System.Collections.Generic;

    using UnityEngine;

    /// <summary>
    ///     Spatial quadtree for hotspot lookup acceleration.
    ///     Supports efficient spatial queries to reduce search time for hotspot scanning.
    /// </summary>
    public class HotspotQuadtree
    {
        private const int MaxDepth = 6;

        private const int MaxPerNode = 8;

        private readonly Node _root;

        /// <summary>
        ///     Initializes a new quadtree with the specified center and world size.
        /// </summary>
        public HotspotQuadtree(Vector2 center, float size)
        {
            var half = size * 0.5f;
            this._root = new Node(new Rect(center.x - half, center.y - half, size, size), 0);
        }

        /// <summary>
        ///     Inserts a hotspot into the spatial quadtree.
        /// </summary>
        public void Insert(HotspotRegistry.Hotspot hotspot)
        {
            if (hotspot == null)
                return;

            this.Insert(this._root, hotspot);
        }

        /// <summary>
        ///     Returns all hotspots within a given world-space radius of a point.
        /// </summary>
        public List<HotspotRegistry.Hotspot> Query(
            Vector3 worldPosition,
            float radius,
            Predicate<HotspotRegistry.Hotspot>? filter = null)
        {
            var result = new List<HotspotRegistry.Hotspot>(16);
            var radiusSq = radius * radius;
            this.Query(this._root, worldPosition, radiusSq, result, filter);
            return result;
        }

        private void Insert(Node node, HotspotRegistry.Hotspot hotspot)
        {
            Vector2 pos2D = new(hotspot.Position.x, hotspot.Position.z);
            if (!node.Bounds.Contains(pos2D))
                return;

            if (node.IsLeaf)
            {
                node.Points.Add(hotspot);

                if (node.Points.Count > MaxPerNode && node.Depth < MaxDepth)
                {
                    this.Subdivide(node);

                    if (node.Children != null)
                    {
                        foreach (var point in node.Points)
                            for (var j = 0; j < 4; j++)
                                this.Insert(node.Children[j], point);

                        node.Points.Clear();
                    }
                }
            }
            else
            {
                if (node.Children != null)
                    for (var i = 0; i < 4; i++)
                        this.Insert(node.Children[i], hotspot);
            }
        }

        private void Query(
            Node node,
            Vector3 worldPosition,
            float radiusSq,
            List<HotspotRegistry.Hotspot> result,
            Predicate<HotspotRegistry.Hotspot>? filter)
        {
            Vector2 pos2D = new(worldPosition.x, worldPosition.z);
            var radius = Mathf.Sqrt(radiusSq);
            Rect queryRect = new(pos2D.x - radius, pos2D.y - radius, radius * 2f, radius * 2f);

            if (!node.Bounds.Overlaps(queryRect))
                return;

            if (node.IsLeaf)
            {
                foreach (var h in node.Points)
                    if ((h.Position - worldPosition).sqrMagnitude <= radiusSq && (filter == null || filter(h)))
                        result.Add(h);
            }
            else if (node.Children != null)
            {
                for (var i = 0; i < 4; i++) this.Query(node.Children[i], worldPosition, radiusSq, result, filter);
            }
        }

        private void Subdivide(Node node)
        {
            node.Children = new Node[4];

            var halfW = node.Bounds.width * 0.5f;
            var halfH = node.Bounds.height * 0.5f;
            var x = node.Bounds.x;
            var y = node.Bounds.y;
            var d = node.Depth + 1;

            node.Children[0] = new Node(new Rect(x, y, halfW, halfH), d); // Bottom Left
            node.Children[1] = new Node(new Rect(x + halfW, y, halfW, halfH), d); // Bottom Right
            node.Children[2] = new Node(new Rect(x, y + halfH, halfW, halfH), d); // Top Left
            node.Children[3] = new Node(new Rect(x + halfW, y + halfH, halfW, halfH), d); // Top Right
        }

        private class Node
        {
            public readonly Rect Bounds;

            public readonly int Depth;

            public readonly List<HotspotRegistry.Hotspot> Points;

            public Node[]? Children;

            public Node(Rect bounds, int depth)
            {
                this.Bounds = bounds;
                this.Depth = depth;
                this.Points = new List<HotspotRegistry.Hotspot>(8);
            }

            public bool IsLeaf => this.Children == null;
        }
    }
}