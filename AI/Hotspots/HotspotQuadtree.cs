#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    /// <summary>
    /// Spatial quadtree for hotspot lookup acceleration.
    /// Supports efficient spatial queries to reduce search time for hotspot scanning.
    /// </summary>
    public class HotspotQuadtree
    {
        #region Internal Node Class

        private class Node
        {
            public readonly Rect Bounds;
            public readonly List<HotspotRegistry.Hotspot> Points;
            public Node[]? Children;
            public readonly int Depth;

            public Node(Rect bounds, int depth)
            {
                Bounds = bounds;
                Depth = depth;
                Points = new List<HotspotRegistry.Hotspot>(8);
            }

            public bool IsLeaf => Children == null;
        }

        #endregion

        #region Constants

        private const int MaxDepth = 6;
        private const int MaxPerNode = 8;

        #endregion

        #region Fields

        private readonly Node _root;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new quadtree with the specified center and world size.
        /// </summary>
        public HotspotQuadtree(Vector2 center, float size)
        {
            float half = size * 0.5f;
            _root = new Node(new Rect(center.x - half, center.y - half, size, size), 0);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Inserts a hotspot into the spatial quadtree.
        /// </summary>
        public void Insert(HotspotRegistry.Hotspot hotspot)
        {
            if (hotspot == null)
                return;

            Insert(_root, hotspot);
        }

        /// <summary>
        /// Returns all hotspots within a given world-space radius of a point.
        /// </summary>
        public List<HotspotRegistry.Hotspot> Query(Vector3 worldPosition, float radius, Predicate<HotspotRegistry.Hotspot>? filter = null)
        {
            List<HotspotRegistry.Hotspot> result = new List<HotspotRegistry.Hotspot>(16);
            float radiusSq = radius * radius;

            Query(_root, worldPosition, radiusSq, result, filter);
            return result;
        }

        #endregion

        #region Internal Insert

        /// <summary>
        /// Inserts a hotspot into the quadtree node, subdividing as needed.
        /// </summary>
        /// <param name="node">The current node to evaluate.</param>
        /// <param name="hotspot">The hotspot to insert.</param>
        private void Insert(Node node, HotspotRegistry.Hotspot hotspot)
        {
            Vector2 pos2D = new Vector2(hotspot.Position.x, hotspot.Position.z);
            if (!node.Bounds.Contains(pos2D))
                return;

            if (node.IsLeaf)
            {
                node.Points.Add(hotspot);

                if (node.Points.Count > MaxPerNode && node.Depth < MaxDepth)
                {
                    Subdivide(node);

                    if (node.Children != null)
                    {
                        int count = node.Points.Count;
                        for (int i = 0; i < count; i++)
                        {
                            HotspotRegistry.Hotspot point = node.Points[i];
                            for (int j = 0; j < node.Children.Length; j++)
                            {
                                Node child = node.Children[j];
                                if (child != null)
                                {
                                    Insert(child, point);
                                }
                            }
                        }

                        node.Points.Clear();
                    }
                }
            }
            else
            {
                if (node.Children != null)
                {
                    for (int i = 0; i < node.Children.Length; i++)
                    {
                        Node child = node.Children[i];
                        if (child != null)
                        {
                            Insert(child, hotspot);
                        }
                    }
                }
            }
        }



        #endregion

        #region Internal Subdivide

        private void Subdivide(Node node)
        {
            node.Children = new Node[4];

            float halfW = node.Bounds.width * 0.5f;
            float halfH = node.Bounds.height * 0.5f;
            float x = node.Bounds.x;
            float y = node.Bounds.y;
            int d = node.Depth + 1;

            node.Children[0] = new Node(new Rect(x, y, halfW, halfH), d);                     // Bottom Left
            node.Children[1] = new Node(new Rect(x + halfW, y, halfW, halfH), d);             // Bottom Right
            node.Children[2] = new Node(new Rect(x, y + halfH, halfW, halfH), d);             // Top Left
            node.Children[3] = new Node(new Rect(x + halfW, y + halfH, halfW, halfH), d);     // Top Right
        }

        #endregion

        #region Internal Query

        private void Query(Node node, Vector3 worldPosition, float radiusSq, List<HotspotRegistry.Hotspot> result, Predicate<HotspotRegistry.Hotspot>? filter)
        {
            Vector2 pos2D = new Vector2(worldPosition.x, worldPosition.z);
            float radius = Mathf.Sqrt(radiusSq);
            Rect queryRect = new Rect(pos2D.x - radius, pos2D.y - radius, radius * 2f, radius * 2f);

            if (!node.Bounds.Overlaps(queryRect))
                return;

            if (node.IsLeaf)
            {
                for (int i = 0; i < node.Points.Count; i++)
                {
                    HotspotRegistry.Hotspot h = node.Points[i];
                    if ((h.Position - worldPosition).sqrMagnitude <= radiusSq &&
                        (filter == null || filter(h)))
                    {
                        result.Add(h);
                    }
                }
            }
            else if (node.Children != null)
            {
                for (int i = 0; i < node.Children.Length; i++)
                {
                    Query(node.Children[i], worldPosition, radiusSq, result, filter);
                }
            }
        }

        #endregion
    }
}
