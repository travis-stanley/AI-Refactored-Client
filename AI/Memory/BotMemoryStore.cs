#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Central memory tracker for bot AI. Handles dynamic danger zones and sound memory.
    /// </summary>
    public static class BotMemoryStore
    {
        private static readonly List<DangerZone> _zones = new(64); // Pre-allocated zone buffer
        private static readonly Dictionary<string, HeardSound> _heardSounds = new(64); // Per-profile auditory memory

        private const int MaxZones = 256;

        #region Danger Zone Tracking

        /// <summary>
        /// Adds a danger zone to memory for bots to avoid or react to.
        /// </summary>
        /// <param name="mapId">Map identifier (e.g. bigmap, factory4_day).</param>
        /// <param name="position">World position of the threat.</param>
        /// <param name="type">Type of danger (panic, flash, etc).</param>
        /// <param name="radius">Effective radius of the threat.</param>
        public static void AddDangerZone(string mapId, Vector3 position, DangerTriggerType type, float radius)
        {
            if (string.IsNullOrEmpty(mapId))
                return;

            if (_zones.Count >= MaxZones)
                _zones.RemoveAt(0); // FIFO logic to cap memory usage

            _zones.Add(new DangerZone(mapId, position, type, radius));
        }

        /// <summary>
        /// Gets all danger zones relevant to a given map.
        /// Uses pooled list for efficiency.
        /// </summary>
        /// <param name="mapId">Map ID.</param>
        /// <returns>List of active zones (rented).</returns>
        public static List<DangerZone> GetZonesForMap(string mapId)
        {
            List<DangerZone> results = ListPool<DangerZone>.Rent();

            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i].Map == mapId)
                    results.Add(_zones[i]);
            }

            return results;
        }

        /// <summary>
        /// Clears all stored zones from memory.
        /// </summary>
        public static void ClearZones()
        {
            _zones.Clear();
        }

        #endregion

        #region Auditory Memory

        /// <summary>
        /// Stores the most recent heard sound position and time for a given bot.
        /// </summary>
        /// <param name="profileId">Bot's profile ID.</param>
        /// <param name="position">Sound position.</param>
        /// <param name="time">Time heard.</param>
        public static void AddHeardSound(string profileId, Vector3 position, float time)
        {
            if (string.IsNullOrEmpty(profileId))
                return;

            _heardSounds[profileId] = new HeardSound(position, time);
        }

        /// <summary>
        /// Checks if a bot has recently heard a sound.
        /// </summary>
        /// <param name="profileId">Bot profile ID.</param>
        /// <param name="sound">Out result.</param>
        /// <returns>True if sound is found.</returns>
        public static bool TryGetHeardSound(string profileId, out HeardSound sound)
        {
            return _heardSounds.TryGetValue(profileId, out sound);
        }

        /// <summary>
        /// Removes the remembered sound for a bot.
        /// </summary>
        /// <param name="profileId">Bot profile ID.</param>
        public static void ClearHeardSound(string profileId)
        {
            _heardSounds.Remove(profileId);
        }

        /// <summary>
        /// Clears all stored sound memory for all bots.
        /// </summary>
        public static void ClearAllHeardSounds()
        {
            _heardSounds.Clear();
        }

        #endregion

        #region Data Structures

        /// <summary>
        /// Represents a remembered sound position and timestamp.
        /// </summary>
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

        #endregion
    }

    /// <summary>
    /// Type of danger zone used to influence AI fallback or caution logic.
    /// </summary>
    public enum DangerTriggerType
    {
        Panic,
        Flash,
        Suppression,
        Grenade
    }

    /// <summary>
    /// Lightweight object pool for temporary lists.
    /// </summary>
    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> _pool = new();

        /// <summary>
        /// Rents a pooled list. Call Return() when done.
        /// </summary>
        public static List<T> Rent()
        {
            if (_pool.Count > 0)
            {
                var list = _pool.Pop();
                list.Clear();
                return list;
            }

            return new List<T>();
        }

        /// <summary>
        /// Returns a list back to the pool.
        /// </summary>
        public static void Return(List<T> list)
        {
            list.Clear();
            _pool.Push(list);
        }
    }
}
