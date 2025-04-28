#nullable enable

namespace AIRefactored.AI.Hotspots
{
    using System;
    using System.Collections.Generic;

    using UnityEngine;

    /// <summary>
    ///     Uniform grid-based spatial index for fast hotspot lookups in small or flat maps.
    ///     Used as a fallback when quadtree is unnecessary or too sparse.
    /// </summary>
    public class HotspotSpatialGrid
    {
        private readonly float _cellSize;

        private readonly Dictionary<Vector2Int, List<HotspotRegistry.Hotspot>> _grid = new(128);

        /// <summary>
        ///     Constructs a new spatial grid with the given cell resolution.
        /// </summary>
        /// <param name="cellSize">Minimum 1f recommended. Determines lookup resolution.</param>
        public HotspotSpatialGrid(float cellSize = 10f)
        {
            this._cellSize = Mathf.Max(1f, cellSize);
        }

        /// <summary>
        ///     Inserts a hotspot into the spatial index.
        /// </summary>
        /// <param name="h">Hotspot to insert.</param>
        public void Insert(HotspotRegistry.Hotspot h)
        {
            var cell = this.WorldToCell(h.Position);

            List<HotspotRegistry.Hotspot> list;
            if (!this._grid.TryGetValue(cell, out list))
            {
                list = new List<HotspotRegistry.Hotspot>(4);
                this._grid[cell] = list;
            }

            list.Add(h);
        }

        /// <summary>
        ///     Returns all hotspots within radius of a given world position.
        /// </summary>
        /// <param name="worldPos">World-space origin for the query.</param>
        /// <param name="radius">Radius in meters.</param>
        /// <param name="filter">Optional predicate to restrict results.</param>
        public List<HotspotRegistry.Hotspot> Query(
            Vector3 worldPos,
            float radius,
            Predicate<HotspotRegistry.Hotspot>? filter = null)
        {
            var results = new List<HotspotRegistry.Hotspot>(16);
            var radiusSq = radius * radius;

            var center = this.WorldToCell(worldPos);
            var cellRadius = Mathf.CeilToInt(radius / this._cellSize);

            for (var dx = -cellRadius; dx <= cellRadius; dx++)
            for (var dz = -cellRadius; dz <= cellRadius; dz++)
            {
                var check = new Vector2Int(center.x + dx, center.y + dz);
                List<HotspotRegistry.Hotspot> list;
                if (this._grid.TryGetValue(check, out list))
                    for (var i = 0; i < list.Count; i++)
                    {
                        var h = list[i];
                        if ((h.Position - worldPos).sqrMagnitude <= radiusSq && (filter == null || filter(h)))
                            results.Add(h);
                    }
            }

            return results;
        }

        private Vector2Int WorldToCell(Vector3 worldPos)
        {
            var x = Mathf.FloorToInt(worldPos.x / this._cellSize);
            var z = Mathf.FloorToInt(worldPos.z / this._cellSize);
            return new Vector2Int(x, z);
        }
    }
}