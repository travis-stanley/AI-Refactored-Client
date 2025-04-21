#nullable enable

using AIRefactored.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Central memory system for tracking sound sources, damage sources, and danger zones.
    /// Used by AI to simulate tactical recall and perception over time.
    /// </summary>
    public static class BotMemoryStore
    {
        #region Fields

        private static readonly List<DangerZone> _zones = new List<DangerZone>(64);
        private static readonly Dictionary<string, HeardSound> _heardSounds = new Dictionary<string, HeardSound>(64);
        private static readonly Dictionary<string, LastHitInfo> _lastHitSources = new Dictionary<string, LastHitInfo>(64);

        private const int MaxZones = 256;
        private const float HitMemoryDuration = 10f;
        private const float DangerZoneTTL = 45f;

        #endregion

        #region Danger Zone Tracking

        /// <summary>
        /// Adds a danger zone to memory for the specified map.
        /// </summary>
        public static void AddDangerZone(string mapId, Vector3 position, DangerTriggerType type, float radius)
        {
            if (string.IsNullOrEmpty(mapId))
                return;

            if (_zones.Count >= MaxZones)
                _zones.RemoveAt(0);

            _zones.Add(new DangerZone(mapId, position, type, radius, Time.time));
        }

        /// <summary>
        /// Returns all active danger zones for the given map.
        /// </summary>
        public static List<DangerZone> GetZonesForMap(string mapId)
        {
            float now = Time.time;
            List<DangerZone> result = ListPool<DangerZone>.Rent();

            for (int i = 0; i < _zones.Count; i++)
            {
                var zone = _zones[i];
                if (zone.Map == mapId && now - zone.Timestamp <= DangerZoneTTL)
                    result.Add(zone);
            }

            return result;
        }

        /// <summary>
        /// Clears all remembered danger zones from memory.
        /// </summary>
        public static void ClearZones() => _zones.Clear();

        #endregion

        #region Auditory Memory

        /// <summary>
        /// Records a sound heard by a bot.
        /// </summary>
        public static void AddHeardSound(string profileId, Vector3 position, float time)
        {
            if (!string.IsNullOrEmpty(profileId))
                _heardSounds[profileId] = new HeardSound(position, time);
        }

        /// <summary>
        /// Attempts to retrieve the last heard sound from memory.
        /// </summary>
        public static bool TryGetHeardSound(string profileId, out HeardSound sound) =>
            _heardSounds.TryGetValue(profileId, out sound);

        /// <summary>
        /// Removes the heard sound associated with the given profile.
        /// </summary>
        public static void ClearHeardSound(string profileId) =>
            _heardSounds.Remove(profileId);

        /// <summary>
        /// Wipes all heard sound memory for all bots.
        /// </summary>
        public static void ClearAllHeardSounds() => _heardSounds.Clear();

        /// <summary>
        /// Finds all alive players within the specified radius.
        /// </summary>
        public static List<Player> GetNearbyPlayers(Vector3 origin, float radius = 40f)
        {
            var result = new List<Player>(8);
            var players = GameWorldHandler.GetAllAlivePlayers();

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                    continue;

                float dist = Vector3.Distance(origin, player.Position);
                if (dist <= radius)
                    result.Add(player);
            }

            return result;
        }

        #endregion

        #region Last Hit Source Memory

        /// <summary>
        /// Records who last hit the given bot and when.
        /// </summary>
        public static void RegisterLastHitSource(string victimProfileId, string attackerProfileId)
        {
            if (!string.IsNullOrEmpty(victimProfileId) && !string.IsNullOrEmpty(attackerProfileId))
                _lastHitSources[victimProfileId] = new LastHitInfo(attackerProfileId, Time.time);
        }

        /// <summary>
        /// Determines if the attacker recently hit the given victim.
        /// </summary>
        public static bool WasRecentlyHitBy(string victimProfileId, string attackerProfileId)
        {
            if (_lastHitSources.TryGetValue(victimProfileId, out var hit))
                return hit.AttackerId == attackerProfileId && (Time.time - hit.Time) <= HitMemoryDuration;

            return false;
        }

        /// <summary>
        /// Clears all remembered last hit source data.
        /// </summary>
        public static void ClearHitSources() => _lastHitSources.Clear();

        #endregion

        #region Data Types

        /// <summary>
        /// Represents an area of perceived danger by bots.
        /// </summary>
        public struct DangerZone
        {
            public string Map;
            public Vector3 Position;
            public DangerTriggerType Type;
            public float Radius;
            public float Timestamp;

            public DangerZone(string map, Vector3 pos, DangerTriggerType type, float radius, float timestamp)
            {
                Map = map;
                Position = pos;
                Type = type;
                Radius = radius;
                Timestamp = timestamp;
            }
        }

        /// <summary>
        /// Represents a heard sound from a specific position at a time.
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
        /// Tracks the last attacker and timestamp of damage.
        /// </summary>
        public readonly struct LastHitInfo
        {
            public readonly string AttackerId;
            public readonly float Time;

            public LastHitInfo(string attackerId, float time)
            {
                AttackerId = attackerId;
                Time = time;
            }
        }

        #endregion
    }

    /// <summary>
    /// Defines danger causes used in tactical memory.
    /// </summary>
    public enum DangerTriggerType
    {
        Panic,
        Flash,
        Suppression,
        Grenade
    }

    /// <summary>
    /// Static list pooling utility to reduce GC from hot memory reads.
    /// </summary>
    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> _pool = new Stack<List<T>>(32);

        /// <summary>
        /// Rents a cleared list from the pool.
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
        /// Returns a list to the pool after clearing it.
        /// </summary>
        public static void Return(List<T> list)
        {
            if (list == null)
                return;

            list.Clear();
            _pool.Push(list);
        }
    }
}
