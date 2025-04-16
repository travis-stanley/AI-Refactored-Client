using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    public static class BotMemoryStore
    {
        private static readonly List<DangerZone> _zones = new();

        public static void AddDangerZone(string mapId, Vector3 position, DangerTriggerType type, float radius)
        {
            _zones.Add(new DangerZone(mapId, position, type, radius));
        }

        private struct DangerZone
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
