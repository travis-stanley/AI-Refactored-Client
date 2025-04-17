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
    /// <summary>
    /// Loads and caches patrol hotspots per map for use in dynamic AI routing and mission planning.
    /// </summary>
    public static class HotspotLoader
    {
        #region Fields

        private const string HOTSPOT_FOLDER = "hotspots/";
        private static readonly Dictionary<string, HotspotSet> _cache = new();
        private static bool _loaded = false;
        private static bool _debugLog = false;

        #endregion

        #region Public API

        /// <summary>
        /// Enables or disables debug logging.
        /// </summary>
        public static void EnableDebugLogs(bool enabled) => _debugLog = enabled;

        /// <summary>
        /// Loads all hotspot files from the configured folder into memory.
        /// </summary>
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

        /// <summary>
        /// Retrieves the hotspot set for the current map and filters it for the given bot role.
        /// </summary>
        public static HotspotSet? GetHotspotsForCurrentMap(WildSpawnType role)
        {
            if (!_loaded)
                LoadAll();

            string mapId = Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown";
            return _cache.TryGetValue(mapId, out var set) ? set.FilteredForRole(role) : null;
        }

        /// <summary>
        /// Clears all loaded hotspots and reloads them from disk.
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            _cache.Clear();
            LoadAll();

            if (_debugLog)
                Debug.Log("[AIRefactored] Hotspot data reloaded.");
        }

        /// <summary>
        /// Returns all loaded hotspot sets.
        /// </summary>
        public static IReadOnlyDictionary<string, HotspotSet> GetAll() => _cache;

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Resolves the file system path to the hotspot folder.
        /// </summary>
        private static string ResolveHotspotPath()
        {
            string fallbackPath = Path.Combine("BepInEx", "plugins", "AIRefactored", HOTSPOT_FOLDER);
            string assetPath = Path.Combine(Application.dataPath, HOTSPOT_FOLDER);
            return Directory.Exists(assetPath) ? assetPath : fallbackPath;
        }

        #endregion
    }

    /// <summary>
    /// Represents the hotspot set for a single map.
    /// Contains all zone locations and potential role filtering.
    /// </summary>
    public class HotspotSet
    {
        /// <summary>
        /// The list of 3D world positions representing hotspots.
        /// </summary>
        public List<Vector3> Points = new();

        /// <summary>
        /// Optional: filters hotspots based on bot role (PMC, Scav, etc).
        /// </summary>
        public HotspotSet FilteredForRole(WildSpawnType role)
        {
            // TODO: Implement role-specific filtering logic
            return this;
        }

        /// <summary>
        /// Parses a JSON file into a HotspotSet.
        /// </summary>
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

        /// <summary>
        /// Internal format for reading hotspot JSON files.
        /// </summary>
        private class HotspotFile
        {
            public List<Vector3> hotspots = new();
        }
    }
}
