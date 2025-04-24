#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Navigation
{
    /// <summary>
    /// Spatial grid-based indexing for CustomNavigationPoints.
    /// Used to accelerate nearby queries over large maps.
    /// </summary>
    public class SpatialNavGrid
    {
        #region Config

        private readonly float _cellSize;
        private readonly Dictionary<Vector2Int, List<CustomNavigationPoint>> _grid;

        #endregion

        #region Constructor

        public SpatialNavGrid(float cellSize = 10f)
        {
            _cellSize = Mathf.Max(1f, cellSize);
            _grid = new Dictionary<Vector2Int, List<CustomNavigationPoint>>(256);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Adds a nav point into the appropriate cell.
        /// </summary>
        public void Register(CustomNavigationPoint point)
        {
            var cell = WorldToCell(point.Position);
            if (!_grid.TryGetValue(cell, out var list))
            {
                list = new List<CustomNavigationPoint>(8);
                _grid[cell] = list;
            }

            if (!list.Contains(point))
                list.Add(point);
        }

        /// <summary>
        /// Clears the entire spatial index.
        /// </summary>
        public void Clear()
        {
            _grid.Clear();
        }

        /// <summary>
        /// Returns all navigation points within the given radius of a position.
        /// Optionally filter by predicate.
        /// </summary>
        public List<CustomNavigationPoint> Query(Vector3 position, float radius, Predicate<CustomNavigationPoint>? filter = null)
        {
            float radiusSq = radius * radius;
            var minCell = WorldToCell(position - Vector3.one * radius);
            var maxCell = WorldToCell(position + Vector3.one * radius);

            var result = new List<CustomNavigationPoint>();

            for (int x = minCell.x; x <= maxCell.x; x++)
            {
                for (int y = minCell.y; y <= maxCell.y; y++)
                {
                    var cell = new Vector2Int(x, y);
                    if (!_grid.TryGetValue(cell, out var list))
                        continue;

                    foreach (var point in list)
                    {
                        if (point == null) continue;

                        float distSq = (point.Position - position).sqrMagnitude;
                        if (distSq <= radiusSq && (filter == null || filter(point)))
                            result.Add(point);
                    }
                }
            }

            return result;
        }

        #endregion

        #region Helpers

        private Vector2Int WorldToCell(Vector3 pos)
        {
            int x = Mathf.FloorToInt(pos.x / _cellSize);
            int z = Mathf.FloorToInt(pos.z / _cellSize);
            return new Vector2Int(x, z);
        }

        #endregion
    }
}
