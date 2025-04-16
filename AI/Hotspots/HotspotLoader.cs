#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using AIRefactored.Data;
using Comfort.Common;
using EFT;
using Newtonsoft.Json;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    /// <summary>
    /// Loads and caches patrol hotspot positions per-map.
    /// </summary>
    public static class HotspotLoader
    {
        private const string HOTSPOT_FOLDER = "hotspots/";
        private static readonly Dictionary<string, HotspotSet> _cache = new();
        private static bool _loaded = false;

        /// <summary>
        /// Loads all hotspot files into memory.
        /// </summary>
        public static void LoadAll()
        {
            if (_loaded)
                return;

            string basePath = Path.Combine(Application.dataPath, HOTSPOT_FOLDER);
            if (!Directory.Exists(basePath))
            {
                basePath = Path.Combine("BepInEx", "plugins", "AIRefactored", HOTSPOT_FOLDER);
            }

            if (!Directory.Exists(basePath))
            {
                Debug.LogWarning($"[AIRefactored] Hotspot folder not found: {basePath}");
                return;
            }

            string[] files = Directory.GetFiles(basePath, "*.json");
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    string json = File.ReadAllText(files[i]);
                    var data = JsonConvert.DeserializeObject<HotspotFile>(json);

                    if (data == null || data.hotspots == null || data.hotspots.Count == 0)
                        continue;

                    string map = Path.GetFileNameWithoutExtension(files[i]);
                    _cache[map.ToLowerInvariant()] = new HotspotSet
                    {
                        Points = data.hotspots
                    };

#if UNITY_EDITOR
                    Debug.Log($"[AIRefactored] Loaded {data.hotspots.Count} hotspots for map '{map}'");
#endif
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[AIRefactored] Failed to load hotspots from {files[i]}: {ex.Message}");
                }
            }

            _loaded = true;
        }

        /// <summary>
        /// Returns hotspots for the current map and given WildSpawnType.
        /// </summary>
        public static HotspotSet? GetHotspotsForCurrentMap(WildSpawnType role)
        {
            if (!_loaded)
                LoadAll();

            string map = Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? string.Empty;
            if (!_cache.TryGetValue(map, out var set))
                return null;

            // PMC or non-PMC logic can be added here if needed
            return set;
        }

        /// <summary>
        /// Forces a reload of all hotspot files.
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            _cache.Clear();
            LoadAll();
            Debug.Log("[AIRefactored] Hotspots reloaded.");
        }

        /// <summary>
        /// Internal data structure matching the on-disk JSON format.
        /// </summary>
        private class HotspotFile
        {
            public List<Vector3> hotspots = new();
        }
    }

    /// <summary>
    /// Runtime hotspot set for a map. May be filtered or randomized by other AI modules.
    /// </summary>
    public class HotspotSet
    {
        public List<Vector3> Points = new();
    }
}
