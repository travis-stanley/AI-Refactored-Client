using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    public static class BotMemoryStore
    {
        private static readonly List<DangerZone> _zones = new();
        private const int MaxZones = 256;

        /// <summary>
        /// Adds a danger zone to memory for bots to avoid or react to.
        /// </summary>
        public static void AddDangerZone(string mapId, Vector3 position, DangerTriggerType type, float radius)
        {
            if (string.IsNullOrEmpty(mapId))
                return;

            _zones.Add(new DangerZone(mapId, position, type, radius));

            if (_zones.Count > MaxZones)
                _zones.RemoveAt(0); // FIFO cleanup

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored-Memory] Danger zone added: {type} at {position} (map: {mapId}, r={radius})");
#endif
        }

        /// <summary>
        /// Gets all zones for the current map.
        /// </summary>
        public static List<DangerZone> GetZonesForMap(string mapId)
        {
            var results = new List<DangerZone>();
            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i].Map == mapId)
                    results.Add(_zones[i]);
            }
            return results;
        }

        /// <summary>
        /// Clears all stored zones.
        /// </summary>
        public static void Clear()
        {
            _zones.Clear();
        }

        /// <summary>
        /// Represents a memory of a known danger zone (panic, flash, etc).
        /// </summary>
        public struct DangerZone
        {
            public string Map;
            public Vector3 Position;
            public DangerTriggerType Type;
            public float Radius;

            public DangerZone(string map, Vector3 pos, DangerTriggerType type, float radius)
            {
                Map = map;
                Position = pos;
                Type = type;
                Radius = radius;
            }
        }
    }

    public enum DangerTriggerType
    {
        Panic,
        Flash,
        Suppression,
        Grenade
    }
}
