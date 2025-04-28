#nullable enable

namespace AIRefactored.AI.Memory
{
    using System.Collections.Generic;

    using AIRefactored.Core;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Centralized memory system for bots: sound tracking, hits, and danger zones.
    ///     Mimics tactical recall and perception over short-term windows.
    /// </summary>
    public static class BotMemoryStore
    {
        private const float DangerZoneTTL = 45f;

        private const float HitMemoryDuration = 10f;

        private const int MaxZones = 256;

        private static readonly Dictionary<string, HeardSound> _heardSounds = new(64);

        private static readonly Dictionary<string, LastHitInfo> _lastHitSources = new(64);

        private static readonly List<DangerZone> _zones = new(64);

        public static void AddDangerZone(string? mapId, Vector3 position, DangerTriggerType type, float radius)
        {
            if (_zones.Count >= MaxZones)
                _zones.RemoveAt(0);

            var safeMap = TryGetSafeKey(mapId, out var map) ? map : "unknown";
            _zones.Add(new DangerZone(safeMap, position, type, radius, Time.time));
        }

        public static void AddHeardSound(string? profileId, Vector3 position, float time)
        {
            if (TryGetSafeKey(profileId, out var key))
                _heardSounds[key] = new HeardSound(position, time);
        }

        public static void ClearAllHeardSounds()
        {
            _heardSounds.Clear();
        }

        public static void ClearHeardSound(string? profileId)
        {
            if (TryGetSafeKey(profileId, out var key))
                _heardSounds.Remove(key);
        }

        public static void ClearHitSources()
        {
            _lastHitSources.Clear();
        }

        public static void ClearZones()
        {
            _zones.Clear();
        }

        public static List<Player> GetNearbyPlayers(Vector3 origin, float radius)
        {
            List<Player> result = new(8);
            var players = GameWorldHandler.GetAllAlivePlayers();

            foreach (var player in players)
                if (player != null && IsRealPlayer(player))
                {
                    var dist = Vector3.Distance(origin, player.Position);
                    if (dist <= radius)
                        result.Add(player);
                }

            return result;
        }

        public static List<DangerZone> GetZonesForMap(string? mapId)
        {
            var result = ListPool<DangerZone>.Rent();
            if (!TryGetSafeKey(mapId, out var map))
                return result;

            var now = Time.time;
            foreach (var zone in _zones)
                if (zone.Map == map && now - zone.Timestamp <= DangerZoneTTL)
                    result.Add(zone);

            return result;
        }

        public static bool IsPositionInDangerZone(string? mapId, Vector3 position)
        {
            if (!TryGetSafeKey(mapId, out var map))
                return false;

            var now = Time.time;
            foreach (var zone in _zones)
                if (zone.Map == map && now - zone.Timestamp <= DangerZoneTTL)
                {
                    var sqrDist = (zone.Position - position).sqrMagnitude;
                    if (sqrDist <= zone.Radius * zone.Radius)
                        return true;
                }

            return false;
        }

        public static void RegisterLastHitSource(string? victimProfileId, string? attackerProfileId)
        {
            if (TryGetSafeKey(victimProfileId, out var victim) && TryGetSafeKey(attackerProfileId, out var attacker))
                _lastHitSources[victim] = new LastHitInfo(attacker, Time.time);
        }

        public static bool TryGetHeardSound(string? profileId, out HeardSound sound)
        {
            sound = default;
            return TryGetSafeKey(profileId, out var key) && _heardSounds.TryGetValue(key, out sound);
        }

        public static bool WasRecentlyHitBy(string? victimProfileId, string? attackerProfileId)
        {
            if (!TryGetSafeKey(victimProfileId, out var victim) || !TryGetSafeKey(attackerProfileId, out var attacker))
                return false;

            return _lastHitSources.TryGetValue(victim, out var hit) && hit.AttackerId == attacker
                                                                    && Time.time - hit.Time <= HitMemoryDuration;
        }

        private static bool IsRealPlayer(Player player)
        {
            return player.AIData == null || player.AIData.IsAI == false;
        }

        /// <summary>
        ///     Attempts to normalize and validate a profile ID string.
        ///     Ensures result is non-null, trimmed, and safe for use as a dictionary key.
        /// </summary>
        /// <param name="profileId">Nullable profile ID to validate.</param>
        /// <param name="key">Normalized output key if valid.</param>
        /// <returns>True if key is valid and safe for use.</returns>
        private static bool TryGetSafeKey(string? profileId, out string key)
        {
            if (profileId == null)
            {
                key = string.Empty;
                return false;
            }

            var trimmed = profileId.Trim();
            if (trimmed.Length == 0)
            {
                key = string.Empty;
                return false;
            }

            key = trimmed;
            return true;
        }

        public struct DangerZone
        {
            public string Map;

            public Vector3 Position;

            public DangerTriggerType Type;

            public float Radius;

            public float Timestamp;

            public DangerZone(string map, Vector3 position, DangerTriggerType type, float radius, float timestamp)
            {
                this.Map = map;
                this.Position = position;
                this.Type = type;
                this.Radius = radius;
                this.Timestamp = timestamp;
            }
        }

        public readonly struct HeardSound
        {
            public readonly Vector3 Position;

            public readonly float Time;

            public HeardSound(Vector3 position, float time)
            {
                this.Position = position;
                this.Time = time;
            }
        }

        public readonly struct LastHitInfo
        {
            public readonly string AttackerId;

            public readonly float Time;

            public LastHitInfo(string attackerId, float time)
            {
                this.AttackerId = attackerId;
                this.Time = time;
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

    internal static class ListPool<T>
    {
        private static readonly Stack<List<T>> Pool = new(32);

        public static List<T> Rent()
        {
            return Pool.Count > 0 ? Pool.Pop() : new List<T>();
        }

        public static void Return(List<T>? list)
        {
            if (list == null) return;
            list.Clear();
            Pool.Push(list);
        }
    }
}