#nullable enable

using AIRefactored.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Central memory tracker for bot AI. Handles dynamic danger zones, sound memory, and hit source tracking.
    /// </summary>
    public static class BotMemoryStore
    {
        private static readonly List<DangerZone> _zones = new List<DangerZone>(64);
        private static readonly Dictionary<string, HeardSound> _heardSounds = new Dictionary<string, HeardSound>(64);
        private static readonly Dictionary<string, LastHitInfo> _lastHitSources = new Dictionary<string, LastHitInfo>(64);

        private const int MaxZones = 256;
        private const float HitMemoryDuration = 10f;

        #region Danger Zone Tracking

        public static void AddDangerZone(string mapId, Vector3 position, DangerTriggerType type, float radius)
        {
            if (string.IsNullOrEmpty(mapId))
                return;

            if (_zones.Count >= MaxZones)
                _zones.RemoveAt(0); // FIFO eviction

            _zones.Add(new DangerZone(mapId, position, type, radius));
        }

        public static List<DangerZone> GetZonesForMap(string mapId)
        {
            List<DangerZone> result = ListPool<DangerZone>.Rent();

            for (int i = 0; i < _zones.Count; i++)
            {
                if (_zones[i].Map == mapId)
                    result.Add(_zones[i]);
            }

            return result;
        }

        public static void ClearZones()
        {
            _zones.Clear();
        }

        #endregion

        #region Auditory Memory

        public static void AddHeardSound(string profileId, Vector3 position, float time)
        {
            if (!string.IsNullOrEmpty(profileId))
            {
                _heardSounds[profileId] = new HeardSound(position, time);
            }
        }

        public static bool TryGetHeardSound(string profileId, out HeardSound sound)
        {
            return _heardSounds.TryGetValue(profileId, out sound);
        }

        public static void ClearHeardSound(string profileId)
        {
            _heardSounds.Remove(profileId);
        }

        public static void ClearAllHeardSounds()
        {
            _heardSounds.Clear();
        }

        public static List<Player> GetNearbyPlayers(Vector3 origin, float radius = 40f)
        {
            List<Player> result = new List<Player>(8);

            foreach (var player in GameWorldHandler.GetAllAlivePlayers())
            {
                if (player == null)
                    continue;

                float dist = Vector3.Distance(origin, player.Position);
                if (dist <= radius)
                    result.Add(player);
            }

            return result;
        }

        #endregion

        #region Last Hit Source Tracking

        /// <summary>
        /// Records who hit this bot most recently and when.
        /// </summary>
        public static void RegisterLastHitSource(string victimProfileId, string attackerProfileId)
        {
            if (!string.IsNullOrEmpty(victimProfileId) && !string.IsNullOrEmpty(attackerProfileId))
            {
                _lastHitSources[victimProfileId] = new LastHitInfo(attackerProfileId, Time.time);
            }
        }

        /// <summary>
        /// Returns true if the given attacker hit this bot recently.
        /// </summary>
        public static bool WasRecentlyHitBy(string victimProfileId, string attackerProfileId)
        {
            if (_lastHitSources.TryGetValue(victimProfileId, out var hit))
            {
                return hit.AttackerId == attackerProfileId && (Time.time - hit.Time) <= HitMemoryDuration;
            }

            return false;
        }

        public static void ClearHitSources()
        {
            _lastHitSources.Clear();
        }

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

    public enum DangerTriggerType
    {
        Panic,
        Flash,
        Suppression,
        Grenade
    }

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
