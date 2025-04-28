#nullable enable

namespace AIRefactored.AI.Hotspots
{
    using System.Collections.Generic;

    using UnityEngine;

    /// <summary>
    ///     Tracks recently visited hotspot positions on a per-map basis.
    ///     Prevents bots from repeatedly targeting the same tactical zones.
    /// </summary>
    internal static class HotspotMemory
    {
        // Structure: mapId → (position → lastVisitTime)
        private static readonly Dictionary<string, Dictionary<Vector3, float>> _visited = new(8);

        /// <summary>
        ///     Clears all stored visited hotspots across all maps.
        ///     Should be called on raid or map unload.
        /// </summary>
        public static void Clear()
        {
            _visited.Clear();
        }

        /// <summary>
        ///     Lightweight check if a hotspot was ever marked visited.
        /// </summary>
        /// <param name="mapId">Map identifier.</param>
        /// <param name="position">Hotspot location.</param>
        /// <returns>1 if visited at least once; 0 otherwise.</returns>
        public static float GetVisitCount(string mapId, Vector3 position)
        {
            return WasRecentlyVisited(mapId, position, float.MaxValue) ? 1f : 0f;
        }

        /// <summary>
        ///     Marks a hotspot position as visited at the current time.
        /// </summary>
        /// <param name="mapId">The map identifier (e.g., "factory4_day").</param>
        /// <param name="position">The world-space position of the hotspot.</param>
        public static void MarkVisited(string mapId, Vector3 position)
        {
            if (string.IsNullOrWhiteSpace(mapId))
                return;

            if (!_visited.TryGetValue(mapId, out var mapDict))
            {
                mapDict = new Dictionary<Vector3, float>(32);
                _visited[mapId] = mapDict;
            }

            mapDict[position] = Time.time;
        }

        /// <summary>
        ///     Checks whether a hotspot was visited recently.
        /// </summary>
        /// <param name="mapId">Map ID to query within.</param>
        /// <param name="position">The position of the hotspot.</param>
        /// <param name="cooldown">Duration in seconds within which a visit counts as recent.</param>
        /// <returns>True if visited within cooldown; otherwise false.</returns>
        public static bool WasRecentlyVisited(string mapId, Vector3 position, float cooldown)
        {
            if (string.IsNullOrWhiteSpace(mapId))
                return false;

            if (_visited.TryGetValue(mapId, out var mapDict) && mapDict.TryGetValue(position, out var lastVisitTime))
                return Time.time - lastVisitTime < cooldown;

            return false;
        }
    }
}