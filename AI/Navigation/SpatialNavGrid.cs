#nullable enable

namespace AIRefactored.AI.Navigation
{
    using System;
    using System.Collections.Generic;

    using UnityEngine;

    /// <summary>
    ///     Spatial grid-based indexing for NavPointData.
    ///     Used to accelerate nearby queries over large maps.
    /// </summary>
    public class SpatialNavGrid
    {
        private readonly float _cellSize;

        private readonly Dictionary<Vector2Int, List<NavPointData>> _grid;

        public SpatialNavGrid(float cellSize = 10f)
        {
            this._cellSize = Mathf.Max(1f, cellSize);
            this._grid = new Dictionary<Vector2Int, List<NavPointData>>(256);
        }

        /// <summary>
        ///     Clears the entire spatial index.
        /// </summary>
        public void Clear()
        {
            this._grid.Clear();
        }

        /// <summary>
        ///     Returns all navigation points within the given radius of a position.
        ///     Optionally filter by predicate.
        /// </summary>
        public List<NavPointData> Query(Vector3 position, float radius, Predicate<NavPointData>? filter = null)
        {
            var radiusSq = radius * radius;
            var minCell = this.WorldToCell(position - Vector3.one * radius);
            var maxCell = this.WorldToCell(position + Vector3.one * radius);

            List<NavPointData> result = new();

            for (var x = minCell.x; x <= maxCell.x; x++)
            for (var y = minCell.y; y <= maxCell.y; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!this._grid.TryGetValue(cell, out var list))
                    continue;

                for (var i = 0; i < list.Count; i++)
                {
                    var point = list[i];
                    var distSq = (point.Position - position).sqrMagnitude;
                    if (distSq <= radiusSq && (filter == null || filter(point)))
                        result.Add(point);
                }
            }

            return result;
        }

        /// <summary>
        ///     Adds a nav point into the appropriate cell.
        /// </summary>
        public void Register(NavPointData point)
        {
            var cell = this.WorldToCell(point.Position);
            if (!this._grid.TryGetValue(cell, out var list))
            {
                list = new List<NavPointData>(8);
                this._grid[cell] = list;
            }

            if (!list.Contains(point))
                list.Add(point);
        }

        private Vector2Int WorldToCell(Vector3 pos)
        {
            var x = Mathf.FloorToInt(pos.x / this._cellSize);
            var z = Mathf.FloorToInt(pos.z / this._cellSize);
            return new Vector2Int(x, z);
        }
    }
}