using System.Collections.Generic;
using EFT.Game.Spawning;
using EFT;
using UnityEngine;

namespace AIRefactored.Spawning
{
    /// <summary>
    /// Caches and provides lookup access to all ISpawnPoint instances by ID.
    /// Used for resolving spawn redirection and zone allocation.
    /// </summary>
    public static class SpawnPointRegistry
    {
        private static readonly Dictionary<string, ISpawnPoint> _idToPoint = new();
        private static readonly List<ISpawnPoint> _allPoints = new();
        private static bool _loaded = false;

        /// <summary>
        /// Initialize and cache all spawn points currently available in the map.
        /// </summary>
        public static void LoadAll()
        {
            _idToPoint.Clear();
            _allPoints.Clear();
            _loaded = false;

            var all = GameObject.FindObjectsOfType<MonoBehaviour>();
            foreach (var mono in all)
            {
                if (mono is ISpawnPoint sp && !string.IsNullOrEmpty(sp.Id))
                {
                    _allPoints.Add(sp);
                    _idToPoint[sp.Id] = sp;
                }
            }

            _loaded = true;
            Debug.Log($"[SpawnPointRegistry] Cached {_allPoints.Count} spawn points");
        }

        public static bool TryGetById(string id, out ISpawnPoint? point)
        {
            if (!_loaded) LoadAll();
            return _idToPoint.TryGetValue(id, out point);
        }

        public static List<ISpawnPoint> GetAll()
        {
            if (!_loaded) LoadAll();
            return _allPoints;
        }
    }
}