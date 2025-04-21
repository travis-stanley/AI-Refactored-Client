#nullable enable

using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Hotspots
{
    /// <summary>
    /// Loads and manages tactical hotspot zones from hardcoded in-memory data.
    /// Hotspots simulate high-value areas for loot, missions, and enemy encounters.
    /// </summary>
    public static class HotspotLoader
    {
        #region Fields

        private static HotspotSet? _activeSet;
        private static string _loadedMapId = "none";
        private static bool _debugLog;
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Enables verbose logging for hotspot loading and filtering.
        /// </summary>
        public static void EnableDebugLogs(bool enabled) => _debugLog = enabled;

        /// <summary>
        /// Loads hotspot data for the current map if not already cached.
        /// </summary>
        public static void LoadCurrentMap()
        {
            string mapId = GameWorldHandler.GetCurrentMapName().ToLowerInvariant();
            if (_loadedMapId == mapId)
                return;

            var set = HardcodedHotspots.GetForMap(mapId);
            if (set != null && set.Points.Count > 0)
            {
                _activeSet = set;
                _loadedMapId = mapId;

                if (_debugLog)
                    _log.LogInfo($"[HotspotLoader] ✅ Loaded {set.Points.Count} hotspots for map '{mapId}'");
            }
            else
            {
                _activeSet = null;
                _loadedMapId = "none";

                if (_debugLog)
                    _log.LogWarning($"[HotspotLoader] ⚠️ No hotspots found for map '{mapId}'");
            }
        }

        /// <summary>
        /// Returns the current map's active hotspot set.
        /// </summary>
        public static HotspotSet? GetHotspotsForCurrentMap()
        {
            LoadCurrentMap();
            return _activeSet;
        }

        /// <summary>
        /// Returns all currently loaded hotspot points, if any.
        /// </summary>
        public static IReadOnlyList<Vector3> GetAllHotspotsRaw()
        {
            LoadCurrentMap();
            return _activeSet?.Points ?? new List<Vector3>(0);
        }

        /// <summary>
        /// Clears cached hotspot data. Use on map unload or full reset.
        /// </summary>
        public static void Reset()
        {
            _activeSet = null;
            _loadedMapId = "none";

            if (_debugLog)
                _log.LogInfo("[HotspotLoader] 🔁 Reset current map hotspot cache.");
        }

        #endregion
    }
}
