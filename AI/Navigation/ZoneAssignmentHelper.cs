#nullable enable

namespace AIRefactored.AI.Navigation
{
    using System;
    using System.Collections.Generic;

    using EFT.Game.Spawning;

    using UnityEngine;

    /// <summary>
    ///     Assigns zone names to world positions using IZones and spawn metadata.
    ///     Supports proximity detection, zone weights, and boss presence.
    /// </summary>
    public static class ZoneAssignmentHelper
    {
        private const float BaseBossWeight = 2.5f;

        private const float MaxZoneSnapDistance = 28f;

        private static readonly HashSet<string> _bossZones = new(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, Vector3> _zoneCenters = new(StringComparer.OrdinalIgnoreCase);

        private static readonly List<string> _zoneNames = new(64);

        private static readonly Dictionary<string, List<ISpawnPoint>> _zoneSpawns = new(
            StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, float> _zoneWeights = new(StringComparer.OrdinalIgnoreCase);

        private static IZones? _zones;

        /// <summary>
        ///     Clears all cached zone data (called on world unload or reset).
        /// </summary>
        public static void Clear()
        {
            _zones = null;
            _zoneCenters.Clear();
            _zoneSpawns.Clear();
            _zoneWeights.Clear();
            _bossZones.Clear();
            _zoneNames.Clear();
        }

        /// <summary>
        ///     Returns all known zone names.
        /// </summary>
        public static IReadOnlyList<string> GetAllZoneNames()
        {
            return _zoneNames;
        }

        /// <summary>
        ///     Returns the nearest known zone name based on proximity to a position.
        /// </summary>
        public static string GetNearestZone(Vector3 position)
        {
            var best = "unassigned";
            var bestDistSq = MaxZoneSnapDistance * MaxZoneSnapDistance;

            foreach (var pair in _zoneCenters)
            {
                var distSq = (pair.Value - position).sqrMagnitude;
                if (distSq < bestDistSq)
                {
                    best = pair.Key;
                    bestDistSq = distSq;
                }
            }

            return best;
        }

        /// <summary>
        ///     Gets all spawn points registered under the zone.
        /// </summary>
        public static List<ISpawnPoint> GetSpawnPoints(string zone)
        {
            return _zoneSpawns.TryGetValue(zone, out var list) ? list : new List<ISpawnPoint>();
        }

        /// <summary>
        ///     Returns the average spawn point location for a zone.
        /// </summary>
        public static Vector3 GetZoneCenter(string zone)
        {
            return _zoneCenters.TryGetValue(zone, out var pos) ? pos : Vector3.zero;
        }

        /// <summary>
        ///     Returns the tactical weight (risk/value) of a zone.
        /// </summary>
        public static float GetZoneWeight(string zone)
        {
            return _zoneWeights.TryGetValue(zone, out var value) ? value : 1f;
        }

        /// <summary>
        ///     Initializes zone metadata using IZones reference and populates all caches.
        /// </summary>
        public static void Initialize(IZones zones, bool includeSnipingZones = true)
        {
            _zones = zones ?? throw new ArgumentNullException(nameof(zones));
            _zoneCenters.Clear();
            _zoneSpawns.Clear();
            _zoneWeights.Clear();
            _bossZones.Clear();
            _zoneNames.Clear();

            foreach (var zoneName in zones.ZoneNames(includeSnipingZones))
            {
                _zoneNames.Add(zoneName);

                ISpawnPoint[] spawns = zones.ZoneSpawnPoints(zoneName);
                if (spawns.Length == 0)
                    continue;

                var sum = Vector3.zero;
                List<ISpawnPoint> spawnList = new(spawns.Length);

                foreach (var sp in spawns)
                {
                    sum += sp.Position;
                    spawnList.Add(sp);
                }

                _zoneCenters[zoneName] = sum / spawns.Length;
                _zoneSpawns[zoneName] = spawnList;

                var weight = 1f;

                if (int.TryParse(zoneName, out var zoneId))
                {
                    var botZone = new BotZone { Id = zoneId };
                    var bossPos = zones.GetBossPosition(botZone);

                    if (bossPos.HasValue)
                    {
                        _bossZones.Add(zoneName);
                        weight += BaseBossWeight;
                    }
                }

                _zoneWeights[zoneName] = weight;
            }
        }

        /// <summary>
        ///     Returns true if the specified zone has a known boss presence.
        /// </summary>
        public static bool IsBossZone(string zone)
        {
            return _bossZones.Contains(zone);
        }
    }
}