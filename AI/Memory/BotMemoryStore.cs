#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Central memory tracker for bot AI. Handles dynamic danger zones and sound memory.
    /// </summary>
    public static class BotMemoryStore
    {
        private static readonly List<DangerZone> _zones = new List<DangerZone>(64);
        private static readonly Dictionary<string, HeardSound> _heardSounds = new Dictionary<string, HeardSound>(64);

        private const int MaxZones = 256;

        #region Danger Zone Tracking

        /// <summary>
        /// Adds a danger zone to memory for bots to avoid or react to.
        /// </summary>
        public static void AddDangerZone(string mapId, Vector3 position, DangerTriggerType type, float radius)
        {
            if (string.IsNullOrEmpty(mapId))
                return;

            if (_zones.Count >= MaxZones)
                _zones.RemoveAt(0); // FIFO eviction

            _zones.Add(new DangerZone(mapId, position, type, radius));
        }

        /// <summary>
        /// Gets all active danger zones for the specified map.
        /// </summary>
        public static List<DangerZone> GetZonesForMap(string mapId)
        {
            List<DangerZone> result = ListPool<DangerZone>.Rent();

            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i].Map == mapId)
                    result.Add(_zones[i]);
            }

            return result;
            // Caller should optionally call: ListPool<DangerZone>.Return(result);
        }

        /// <summary>
        /// Clears all stored danger zones.
        /// </summary>
        public static void ClearZones()
        {
            _zones.Clear();
        }

        #endregion

        #region Auditory Memory

        /// <summary>
        /// Stores the most recent heard sound position and time for a bot.
        /// </summary>
        public static void AddHeardSound(string profileId, Vector3 position, float time)
        {
            if (!string.IsNullOrEmpty(profileId))
            {
                _heardSounds[profileId] = new HeardSound(position, time);
            }
        }

        /// <summary>
        /// Retrieves the last heard sound memory (if any) for the given profile.
        /// </summary>
        public static bool TryGetHeardSound(string profileId, out HeardSound sound)
        {
            return _heardSounds.TryGetValue(profileId, out sound);
        }

        /// <summary>
        /// Removes the remembered sound for a bot.
        /// </summary>
        public static void ClearHeardSound(string profileId)
        {
            _heardSounds.Remove(profileId);
        }

        /// <summary>
        /// Clears all auditory memory for all bots.
        /// </summary>
        public static void ClearAllHeardSounds()
        {
            _heardSounds.Clear();
        }

        #endregion

        #region Data Structures

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

        public readonly struct HeardSound
        {
            public readonly Vector3 Position;
            public readonly float Time;

            public HeardSound(Vector3 position, float time)
            {
                Position = position;
                Time = time;
            }
        }

        #endregion
    }

    /// <summary>
    /// Type of threat that caused a danger zone.
    /// </summary>
    public enum DangerTriggerType
    {
        Panic,
        Flash,
        Suppression,
        Grenade
    }

    /// <summary>
    /// Lightweight shared list pool for temporary memory-safe rentals.
    /// </summary>
    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> _pool = new Stack<List<T>>(32);

        public static List<T> Rent()
        {
            if (_pool.Count > 0)
            {
                List<T> list = _pool.Pop();
                list.Clear();
                return list;
            }

            return new List<T>();
        }

        public static void Return(List<T> list)
        {
            if (list == null)
                return;

            list.Clear();
            _pool.Push(list);
        }
    }
}
