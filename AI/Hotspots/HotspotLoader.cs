#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    public static class HotspotLoader
    {
        private const string HOTSPOT_FOLDER = "hotspots/";
        private static readonly Dictionary<string, HotspotSet> _cache = new Dictionary<string, HotspotSet>();
        private static bool _loaded = false;
        private static bool _debugLog = false;

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        public static void EnableDebugLogs(bool enabled)
        {
            _debugLog = enabled;
        }

        /// <summary>
        /// Loads all hotspots from the file system into the cache.
        /// </summary>
        public static void LoadAll()
        {
            if (_loaded)
                return;

            string basePath = ResolveHotspotPath();
            if (!Directory.Exists(basePath))
            {
                _log.LogWarning($"[AIRefactored] Hotspot folder not found at: {basePath}");
                return;
            }

            string[] files = Directory.GetFiles(basePath, "*.json", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                try
                {
                    string json = File.ReadAllText(file);
                    HotspotSet? set = HotspotSet.FromJson(json);
                    if (set == null || set.Points.Count == 0)
                        continue;

                    string map = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    _cache[map] = set;

                    if (_debugLog)
                        _log.LogInfo($"[AIRefactored] Loaded {set.Points.Count} hotspots for map '{map}'");
                }
                catch (Exception ex)
                {
                    _log.LogError($"[AIRefactored] Failed to load hotspots from '{file}': {ex.Message}");
                }
            }

            _loaded = true;
        }

        /// <summary>
        /// Gets the hotspot set for the current map based on the bot's role.
        /// </summary>
        public static HotspotSet? GetHotspotsForCurrentMap(WildSpawnType role)
        {
            if (!_loaded)
                LoadAll();

            string mapId = Singleton<GameWorld>.Instance?.LocationId?.ToLowerInvariant() ?? "unknown";

            if (_cache.TryGetValue(mapId, out var set))
                return set.FilteredForRole(role);

            return null;
        }

        /// <summary>
        /// Reloads the hotspot data from the files.
        /// </summary>
        public static void Reload()
        {
            _loaded = false;
            _cache.Clear();
            LoadAll();

            if (_debugLog)
                _log.LogInfo("[AIRefactored] Hotspot data reloaded.");
        }

        /// <summary>
        /// Gets all cached hotspots.
        /// </summary>
        public static IReadOnlyDictionary<string, HotspotSet> GetAll()
        {
            return _cache;
        }

        /// <summary>
        /// Resolves the path to the hotspot folder.
        /// </summary>
        private static string ResolveHotspotPath()
        {
            string fallbackPath = Path.Combine("BepInEx", "plugins", "AIRefactored", HOTSPOT_FOLDER);
            string assetPath = Path.Combine(Application.dataPath, HOTSPOT_FOLDER);
            return Directory.Exists(assetPath) ? assetPath : fallbackPath;
        }
    }

    public class HotspotSet
    {
        public List<Vector3> Points = new List<Vector3>();

        /// <summary>
        /// Filters the hotspot points for the given role.
        /// </summary>
        public HotspotSet FilteredForRole(WildSpawnType role)
        {
            HotspotSet result = new HotspotSet();
            int count = Points.Count;

            if (count == 0)
                return this;

            if (role == WildSpawnType.pmcBEAR || role == WildSpawnType.pmcUSEC)
            {
                int limit = (int)(count * 0.6f);
                for (int i = 0; i < limit && i < count; i++)
                    result.Points.Add(Points[i]);
            }
            else if (role == WildSpawnType.assault || role == WildSpawnType.cursedAssault)
            {
                int skip = (int)(count * 0.4f);
                for (int i = skip; i < count; i++)
                    result.Points.Add(Points[i]);
            }
            else
            {
                for (int i = 0; i < count; i++)
                    result.Points.Add(Points[i]);
            }

            return result;
        }

        /// <summary>
        /// Deserializes JSON into a HotspotSet.
        /// </summary>
        public static HotspotSet? FromJson(string json)
        {
            try
            {
                HotspotFile? file = JsonConvert.DeserializeObject<HotspotFile>(json);
                if (file == null || file.hotspots == null || file.hotspots.Count == 0)
                    return null;

                HotspotSet set = new HotspotSet();
                for (int i = 0; i < file.hotspots.Count; i++)
                    set.Points.Add(file.hotspots[i]);

                return set;
            }
            catch (Exception ex)
            {
                AIRefactoredController.Logger.LogError($"[AIRefactored] JSON parse error: {ex.Message}");
                return null;
            }
        }

        private class HotspotFile
        {
            public List<Vector3> hotspots = new List<Vector3>();
        }
    }
}
