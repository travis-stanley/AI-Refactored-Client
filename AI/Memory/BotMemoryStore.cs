#nullable enable

using AIRefactored.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Centralized memory system for bots: sound tracking, hits, and danger zones.
    /// Mimics tactical recall and perception over short-term windows.
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

        public static void AddDangerZone(string? mapId, Vector3 position, DangerTriggerType type, float radius)
        {
            string map = string.IsNullOrWhiteSpace(mapId) ? "unknown" : mapId!;
            if (_zones.Count >= MaxZones)
                _zones.RemoveAt(0);

            _zones.Add(new DangerZone(map, position, type, radius, Time.time));
        }

        public static List<DangerZone> GetZonesForMap(string? mapId)
        {
            List<DangerZone> result = ListPool<DangerZone>.Rent();
            if (string.IsNullOrEmpty(mapId))
                return result;

            float now = Time.time;
            for (int i = 0; i < _zones.Count; i++)
            {
                DangerZone zone = _zones[i];
                if (zone.Map == mapId && now - zone.Timestamp <= DangerZoneTTL)
                    result.Add(zone);
            }

            return result;
        }

        public static bool IsPositionInDangerZone(string? mapId, Vector3 position)
        {
            if (string.IsNullOrEmpty(mapId))
                return false;

            float now = Time.time;
            for (int i = 0; i < _zones.Count; i++)
            {
                DangerZone zone = _zones[i];
                if (zone.Map == mapId && now - zone.Timestamp <= DangerZoneTTL)
                {
                    float sqrDist = (zone.Position - position).sqrMagnitude;
                    if (sqrDist <= zone.Radius * zone.Radius)
                        return true;
                }
            }

            return false;
        }

        public static void ClearZones()
        {
            _zones.Clear();
        }

        #endregion

        #region Auditory Memory

        public static void AddHeardSound(string? profileId, Vector3 position, float time)
        {
            if (!string.IsNullOrWhiteSpace(profileId))
                _heardSounds[profileId!] = new HeardSound(position, time);
        }

        public static bool TryGetHeardSound(string? profileId, out HeardSound sound)
        {
            sound = default(HeardSound);
            return !string.IsNullOrWhiteSpace(profileId) && _heardSounds.TryGetValue(profileId!, out sound);
        }

        public static void ClearHeardSound(string? profileId)
        {
            if (!string.IsNullOrWhiteSpace(profileId))
                _heardSounds.Remove(profileId!);
        }

        public static void ClearAllHeardSounds()
        {
            _heardSounds.Clear();
        }

        public static List<Player> GetNearbyPlayers(Vector3 origin, float radius)
        {
            List<Player> result = new List<Player>(8);
            List<Player> players = GameWorldHandler.GetAllAlivePlayers();

            for (int i = 0; i < players.Count; i++)
            {
                Player p = players[i];
                if (p == null || !IsRealPlayer(p))
                    continue;

                float dist = Vector3.Distance(origin, p.Position);
                if (dist <= radius)
                    result.Add(p);
            }

            return result;
        }

        private static bool IsRealPlayer(Player player)
        {
            return player.AIData == null || player.AIData.IsAI == false;
        }

        #endregion

        #region Last Hit Source Memory

        public static void RegisterLastHitSource(string? victimProfileId, string? attackerProfileId)
        {
            if (!string.IsNullOrWhiteSpace(victimProfileId) && !string.IsNullOrWhiteSpace(attackerProfileId))
                _lastHitSources[victimProfileId!] = new LastHitInfo(attackerProfileId!, Time.time);
        }

        public static bool WasRecentlyHitBy(string? victimProfileId, string? attackerProfileId)
        {
            if (string.IsNullOrWhiteSpace(victimProfileId) || string.IsNullOrWhiteSpace(attackerProfileId))
                return false;

            LastHitInfo hit;
            if (_lastHitSources.TryGetValue(victimProfileId!, out hit))
            {
                return hit.AttackerId == attackerProfileId && (Time.time - hit.Time) <= HitMemoryDuration;
            }

            return false;
        }

        public static void ClearHitSources()
        {
            _lastHitSources.Clear();
        }

        #endregion

        #region Structs

        public struct DangerZone
        {
            public string Map;
            public Vector3 Position;
            public DangerTriggerType Type;
            public float Radius;
            public float Timestamp;

            public DangerZone(string map, Vector3 position, DangerTriggerType type, float radius, float timestamp)
            {
                Map = map;
                Position = position;
                Type = type;
                Radius = radius;
                Timestamp = timestamp;
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

    public enum DangerTriggerType
    {
        Panic,
        Flash,
        Suppression,
        Grenade
    }

    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new Stack<List<T>>(32);

        public static List<T> Rent()
        {
            if (Pool.Count > 0)
                return Pool.Pop();
            return new List<T>();
        }

        public static void Return(List<T>? list)
        {
            if (list == null)
                return;

            list.Clear();
            Pool.Push(list);
        }
    }
}
