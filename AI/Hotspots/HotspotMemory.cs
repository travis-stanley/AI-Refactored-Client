#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    /// <summary>
    /// Tracks recently visited hotspot positions on a per-map basis.
    /// Prevents bots from repeatedly targeting the same tactical zones.
    /// </summary>
    internal static class HotspotMemory
    {
        #region Internal State

        // Dictionary structure: mapId → position → lastVisitTime
        private static readonly Dictionary<string, Dictionary<Vector3, float>> _visited =
            new Dictionary<string, Dictionary<Vector3, float>>(8);

        #endregion

        #region Public API

        /// <summary>
        /// Marks a hotspot position as visited for the specified map.
        /// </summary>
        /// <param name="mapId">The identifier of the current map.</param>
        /// <param name="position">The hotspot's world position.</param>
        public static void MarkVisited(string mapId, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(mapId))
                return;

            Dictionary<Vector3, float> mapDict;
            if (!_visited.TryGetValue(mapId, out mapDict))
            {
                mapDict = new Dictionary<Vector3, float>(32);
                _visited[mapId] = mapDict;
            }

            mapDict[position] = Time.time;
        }

        /// <summary>
        /// Returns true if the position was visited within the given cooldown period.
        /// </summary>
        /// <param name="mapId">The map identifier.</param>
        /// <param name="position">Hotspot position to query.</param>
        /// <param name="cooldown">Cooldown in seconds to consider recent.</param>
        public static bool WasRecentlyVisited(string mapId, Vector3 position, float cooldown)
        {
            if (string.IsNullOrWhiteSpace(mapId))
                return false;

            Dictionary<Vector3, float> mapDict;
            if (!_visited.TryGetValue(mapId, out mapDict))
                return false;

            float lastTime;
            if (!mapDict.TryGetValue(position, out lastTime))
                return false;

            return Time.time - lastTime < cooldown;
        }

        /// <summary>
        /// Returns 1 if the position was ever marked as visited, 0 otherwise.
        /// Used for basic "visit frequency" logic in simple scenarios.
        /// </summary>
        public static float GetVisitCount(string mapId, Vector3 position)
        {
            return WasRecentlyVisited(mapId, position, float.MaxValue) ? 1f : 0f;
        }

        /// <summary>
        /// Clears all memory of visited hotspots. Typically called on map unload or session reset.
        /// </summary>
        public static void Clear()
        {
            _visited.Clear();
        }

        #endregion
    }
}
