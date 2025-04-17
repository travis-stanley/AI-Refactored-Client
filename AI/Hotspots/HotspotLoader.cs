#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Comfort.Common;
using EFT;
using Newtonsoft.Json;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    public static class HotspotLoader
    {
        private const string HOTSPOT_FOLDER = "hotspots/";
        private static readonly Dictionary<string, HotspotSet> _cache = new();
        private static bool _loaded = false;
        private static bool _debugLog = false;

        public static void EnableDebugLogs(bool enabled) => _debugLog = enabled;

        public static void LoadAll()
        {
            if (_loaded)
                return;

            string basePath = ResolveHotspotPath();
            if (!Directory.Exists(basePath))
            {
                Debug.LogWarning($"[AIRefactored] Hotspot folder not found at: {basePath}");
                return;
            }

            string[] files = Directory.GetFiles(basePath, "*.json", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                try
                {
                    string json = File.ReadAllText(file);
                    var set = HotspotSet.FromJson(json);

                    if (set == null || set.Points.Count == 0)
                        continue;

                    string map = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    _cache[map] = set;

                    if (_debugLog)
                        Debug.Log($"[AIRefactored] Loaded {set.Points.Count} hotspots for map '{map}'");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AIRefactored] Failed to load hotspots from '{file}': {ex.Message}");
                }
            }

            _loaded = true;
        }

        public static HotspotSet? GetHotspotsForCurrentMap(WildSpawnType role)
        {
            if (!_loaded)
                LoadAll();

            string mapId = Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown";
            return _cache.TryGetValue(mapId, out var set)
                ? set.FilteredForRole(role)
                : null;
        }

        public static void Reload()
        {
            _loaded = false;
            _cache.Clear();
            LoadAll();

            if (_debugLog)
                Debug.Log("[AIRefactored] Hotspot data reloaded.");
        }

        public static IReadOnlyDictionary<string, HotspotSet> GetAll() => _cache;

        private static string ResolveHotspotPath()
        {
            string fallbackPath = Path.Combine("BepInEx", "plugins", "AIRefactored", HOTSPOT_FOLDER);
            string assetPath = Path.Combine(Application.dataPath, HOTSPOT_FOLDER);
            return Directory.Exists(assetPath) ? assetPath : fallbackPath;
        }
    }

    public class HotspotSet
    {
        public List<Vector3> Points = new();

        public HotspotSet FilteredForRole(WildSpawnType role)
        {
            if (Points.Count == 0)
                return this;

            List<Vector3> filtered = new();

            if (role == WildSpawnType.pmcBEAR || role == WildSpawnType.pmcUSEC)
            {
                int count = Mathf.CeilToInt(Points.Count * 0.6f); // Prioritize first 60%
                filtered.AddRange(Points.GetRange(0, count));
            }
            else if (role == WildSpawnType.assault || role == WildSpawnType.cursedAssault)
            {
                int skip = Mathf.FloorToInt(Points.Count * 0.4f); // Skip first 40%
                filtered.AddRange(Points.GetRange(skip, Points.Count - skip));
            }
            else
            {
                filtered.AddRange(Points);
            }

            return new HotspotSet { Points = filtered };
        }

        public static HotspotSet? FromJson(string json)
        {
            try
            {
                var file = JsonConvert.DeserializeObject<HotspotFile>(json);
                if (file?.hotspots == null || file.hotspots.Count == 0)
                    return null;

                return new HotspotSet { Points = file.hotspots };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AIRefactored] JSON parse error: {ex.Message}");
                return null;
            }
        }

        private class HotspotFile
        {
            public List<Vector3> hotspots = new();
        }
    }
}
