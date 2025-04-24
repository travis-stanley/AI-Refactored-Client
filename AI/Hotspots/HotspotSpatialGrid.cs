#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    /// <summary>
    /// Uniform grid-based spatial index for fast hotspot lookups in small or flat maps.
    /// Used as a fallback when quadtree is unnecessary or too sparse.
    /// </summary>
    public class HotspotSpatialGrid
    {
        #region Fields

        private readonly float _cellSize;
        private readonly Dictionary<Vector2Int, List<HotspotRegistry.Hotspot>> _grid = new Dictionary<Vector2Int, List<HotspotRegistry.Hotspot>>(128);

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a new spatial grid with the given cell resolution.
        /// </summary>
        /// <param name="cellSize">Minimum 1f recommended. Determines lookup resolution.</param>
        public HotspotSpatialGrid(float cellSize = 10f)
        {
            _cellSize = Mathf.Max(1f, cellSize);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Inserts a hotspot into the spatial index.
        /// </summary>
        /// <param name="h">Hotspot to insert.</param>
        public void Insert(HotspotRegistry.Hotspot h)
        {
            Vector2Int cell = WorldToCell(h.Position);

            List<HotspotRegistry.Hotspot> list;
            if (!_grid.TryGetValue(cell, out list))
            {
                list = new List<HotspotRegistry.Hotspot>(4);
                _grid[cell] = list;
            }

            list.Add(h);
        }

        /// <summary>
        /// Returns all hotspots within radius of a given world position.
        /// </summary>
        /// <param name="worldPos">World-space origin for the query.</param>
        /// <param name="radius">Radius in meters.</param>
        /// <param name="filter">Optional predicate to restrict results.</param>
        public List<HotspotRegistry.Hotspot> Query(Vector3 worldPos, float radius, Predicate<HotspotRegistry.Hotspot>? filter = null)
        {
            List<HotspotRegistry.Hotspot> results = new List<HotspotRegistry.Hotspot>(16);
            float radiusSq = radius * radius;

            Vector2Int center = WorldToCell(worldPos);
            int cellRadius = Mathf.CeilToInt(radius / _cellSize);

            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                for (int dz = -cellRadius; dz <= cellRadius; dz++)
                {
                    Vector2Int check = new Vector2Int(center.x + dx, center.y + dz);
                    List<HotspotRegistry.Hotspot> list;
                    if (_grid.TryGetValue(check, out list))
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            HotspotRegistry.Hotspot h = list[i];
                            if ((h.Position - worldPos).sqrMagnitude <= radiusSq &&
                                (filter == null || filter(h)))
                            {
                                results.Add(h);
                            }
                        }
                    }
                }
            }

            return results;
        }

        #endregion

        #region Helpers

        private Vector2Int WorldToCell(Vector3 worldPos)
        {
            int x = Mathf.FloorToInt(worldPos.x / _cellSize);
            int z = Mathf.FloorToInt(worldPos.z / _cellSize);
            return new Vector2Int(x, z);
        }

        #endregion
    }
}
