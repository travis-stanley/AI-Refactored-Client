#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Navigation
{
    public class QuadtreeNavGrid
    {
        #region Constants

        private const int MaxPointsPerNode = 8;
        private const int MaxDepth = 6;

        #endregion

        #region Node Definition

        private class Node
        {
            public Rect Bounds;
            public List<CustomNavigationPoint> NavPoints;
            public List<Vector3> RawPoints;
            public Node[]? Children;
            public int Depth;

            public Node(Rect bounds, int depth)
            {
                Bounds = bounds;
                NavPoints = new List<CustomNavigationPoint>(MaxPointsPerNode);
                RawPoints = new List<Vector3>(MaxPointsPerNode);
                Depth = depth;
            }

            public bool IsLeaf => Children == null;
        }

        #endregion

        #region Fields

        private Node _root;

        #endregion

        #region Constructor

        public QuadtreeNavGrid(Vector2 center, float size)
        {
            float half = size * 0.5f;
            _root = new Node(new Rect(center.x - half, center.y - half, size, size), 0);
        }

        #endregion

        #region Public API

        public void Clear()
        {
            _root = new Node(_root.Bounds, 0);
        }

        public void Insert(CustomNavigationPoint point)
        {
            if (point == null) return;
            Insert(_root, point);
        }

        public void Insert(Vector3 point)
        {
            Insert(_root, point);
        }

        public List<CustomNavigationPoint> Query(Vector3 position, float radius, Predicate<CustomNavigationPoint>? filter = null)
        {
            var result = new List<CustomNavigationPoint>();
            Query(_root, position, radius * radius, result, filter);
            return result;
        }

        public List<Vector3> QueryRaw(Vector3 position, float radius, Predicate<Vector3>? filter = null)
        {
            var result = new List<Vector3>();
            QueryRaw(_root, position, radius * radius, result, filter);
            return result;
        }

        #endregion

        #region Internal Insert (NavPoints)

        private void Insert(Node node, CustomNavigationPoint point)
        {
            Vector2 pos2D = new Vector2(point.Position.x, point.Position.z);

            if (!node.Bounds.Contains(pos2D))
                return;

            if (node.IsLeaf)
            {
                node.NavPoints.Add(point);

                if (node.NavPoints.Count > MaxPointsPerNode && node.Depth < MaxDepth)
                {
                    Subdivide(node);
                    ReinsertPoints(node);
                }
            }
            else
            {
                foreach (var child in node.Children!)
                    Insert(child, point);
            }
        }

        private void Insert(Node node, Vector3 point)
        {
            Vector2 pos2D = new Vector2(point.x, point.z);

            if (!node.Bounds.Contains(pos2D))
                return;

            if (node.IsLeaf)
            {
                node.RawPoints.Add(point);

                if (node.RawPoints.Count > MaxPointsPerNode && node.Depth < MaxDepth)
                {
                    Subdivide(node);
                    ReinsertPoints(node);
                }
            }
            else
            {
                foreach (var child in node.Children!)
                    Insert(child, point);
            }
        }

        private void Subdivide(Node node)
        {
            node.Children = new Node[4];
            float halfW = node.Bounds.width * 0.5f;
            float halfH = node.Bounds.height * 0.5f;

            node.Children[0] = new Node(new Rect(node.Bounds.x, node.Bounds.y, halfW, halfH), node.Depth + 1);                      // Bottom Left
            node.Children[1] = new Node(new Rect(node.Bounds.x + halfW, node.Bounds.y, halfW, halfH), node.Depth + 1);              // Bottom Right
            node.Children[2] = new Node(new Rect(node.Bounds.x, node.Bounds.y + halfH, halfW, halfH), node.Depth + 1);              // Top Left
            node.Children[3] = new Node(new Rect(node.Bounds.x + halfW, node.Bounds.y + halfH, halfW, halfH), node.Depth + 1);      // Top Right
        }

        private void ReinsertPoints(Node node)
        {
            var navCopy = node.NavPoints;
            node.NavPoints = new List<CustomNavigationPoint>();

            foreach (var point in navCopy)
            {
                foreach (var child in node.Children!)
                    Insert(child, point);
            }

            var rawCopy = node.RawPoints;
            node.RawPoints = new List<Vector3>();

            foreach (var point in rawCopy)
            {
                foreach (var child in node.Children!)
                    Insert(child, point);
            }
        }

        #endregion

        #region Internal Query

        private void Query(Node node, Vector3 worldPos, float radiusSq, List<CustomNavigationPoint> result, Predicate<CustomNavigationPoint>? filter)
        {
            Vector2 pos2D = new Vector2(worldPos.x, worldPos.z);
            if (!node.Bounds.Overlaps(new Rect(pos2D.x - radiusSq, pos2D.y - radiusSq, radiusSq * 2f, radiusSq * 2f)))
                return;

            if (node.IsLeaf)
            {
                foreach (var point in node.NavPoints)
                {
                    if (point == null) continue;

                    float distSq = (point.Position - worldPos).sqrMagnitude;
                    if (distSq <= radiusSq && (filter == null || filter(point)))
                        result.Add(point);
                }
            }
            else
            {
                foreach (var child in node.Children!)
                    Query(child, worldPos, radiusSq, result, filter);
            }
        }

        private void QueryRaw(Node node, Vector3 worldPos, float radiusSq, List<Vector3> result, Predicate<Vector3>? filter)
        {
            Vector2 pos2D = new Vector2(worldPos.x, worldPos.z);
            if (!node.Bounds.Overlaps(new Rect(pos2D.x - radiusSq, pos2D.y - radiusSq, radiusSq * 2f, radiusSq * 2f)))
                return;

            if (node.IsLeaf)
            {
                foreach (var point in node.RawPoints)
                {
                    float distSq = (point - worldPos).sqrMagnitude;
                    if (distSq <= radiusSq && (filter == null || filter(point)))
                        result.Add(point);
                }
            }
            else
            {
                foreach (var child in node.Children!)
                    QueryRaw(child, worldPos, radiusSq, result, filter);
            }
        }

        #endregion
    }
}
