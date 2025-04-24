#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Core;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Stores recent enemy sightings and cleared safe zones.
    /// Helps bots avoid repeat danger and synchronize fallback intel.
    /// </summary>
    public class BotTacticalMemory
    {
        #region Constants

        private const float MaxMemoryTime = 14f;
        private const float ClearedMemoryDuration = 10f;
        private const float PositionToleranceSqr = 0.25f;
        private const float GridSnapSize = 0.5f;

        #endregion

        #region Fields

        private readonly Dictionary<Vector3, float> _clearedSpots = new Dictionary<Vector3, float>(32, new Vector3EqualityComparer());
        private readonly List<SeenEnemyRecord> _enemyMemoryList = new List<SeenEnemyRecord>(4);
        private Dictionary<string, SeenEnemyRecord> _enemyMemoryById = new Dictionary<string, SeenEnemyRecord>(4);

        private BotComponentCache? _cache;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
        }

        #endregion

        #region Enemy Memory

        public void RecordEnemyPosition(Vector3 position, string? tag = "Generic", string? enemyId = null)
        {
            if (_cache == null || _cache.IsBlinded || (_cache.PanicHandler?.IsPanicking == true))
                return;

            float now = Time.time;
            Vector3 gridPos = SnapToGrid(position);
            string finalTag = tag ?? "Generic";

            if (!string.IsNullOrEmpty(enemyId))
                _enemyMemoryById[enemyId!] = new SeenEnemyRecord(gridPos, now, finalTag);

            for (int i = 0; i < _enemyMemoryList.Count; i++)
            {
                if ((gridPos - _enemyMemoryList[i].Position).sqrMagnitude < PositionToleranceSqr)
                {
                    _enemyMemoryList[i] = new SeenEnemyRecord(gridPos, now, finalTag);
                    return;
                }
            }

            _enemyMemoryList.Add(new SeenEnemyRecord(gridPos, now, finalTag));
        }

        public Vector3? GetRecentEnemyMemory()
        {
            float now = Time.time;
            SeenEnemyRecord? freshest = null;

            for (int i = 0; i < _enemyMemoryList.Count; i++)
            {
                SeenEnemyRecord mem = _enemyMemoryList[i];
                if (now - mem.TimeSeen <= MaxMemoryTime)
                {
                    if (!freshest.HasValue || mem.TimeSeen > freshest.Value.TimeSeen)
                        freshest = mem;
                }
            }

            return freshest?.Position;
        }

        public void CullExpired()
        {
            float now = Time.time;
            _enemyMemoryList.RemoveAll(mem => now - mem.TimeSeen > MaxMemoryTime);

            var toRemove = new List<string>();
            foreach (KeyValuePair<string, SeenEnemyRecord> kvp in _enemyMemoryById)
            {
                if (now - kvp.Value.TimeSeen > MaxMemoryTime)
                    toRemove.Add(kvp.Key);
            }

            for (int i = 0; i < toRemove.Count; i++)
                _enemyMemoryById.Remove(toRemove[i]);
        }

        #endregion

        #region Cleared Zones

        public void MarkCleared(Vector3 position)
        {
            _clearedSpots[SnapToGrid(position)] = Time.time;
        }

        public bool WasRecentlyCleared(Vector3 position)
        {
            Vector3 gridPos = SnapToGrid(position);
            float lastTime;
            return _clearedSpots.TryGetValue(gridPos, out lastTime) && Time.time - lastTime < ClearedMemoryDuration;
        }

        public bool IsZoneUnsafe(Vector3 position)
        {
            if (_cache == null || _cache.Bot == null)
                return false;

            string map = GameWorldHandler.GetCurrentMapName();
            Vector3 gridPos = SnapToGrid(position);
            float now = Time.time;

            // Local enemy memory
            for (int i = 0; i < _enemyMemoryList.Count; i++)
            {
                if ((gridPos - _enemyMemoryList[i].Position).sqrMagnitude < PositionToleranceSqr)
                    return true;
            }

            // Cleared zone re-sweep logic
            foreach (KeyValuePair<Vector3, float> kvp in _clearedSpots)
            {
                if ((kvp.Key - gridPos).sqrMagnitude < PositionToleranceSqr && now - kvp.Value < ClearedMemoryDuration)
                    return true;
            }

            // Global danger memory
            return BotMemoryStore.IsPositionInDangerZone(map, position);
        }

        #endregion

        #region Squad Sync

        public void SyncMemory(Vector3 position, string tag = "AllyEcho")
        {
            RecordEnemyPosition(position, tag);
        }

        public void ShareMemoryWith(List<BotComponentCache> teammates)
        {
            for (int i = 0; i < _enemyMemoryList.Count; i++)
            {
                SeenEnemyRecord record = _enemyMemoryList[i];
                for (int j = 0; j < teammates.Count; j++)
                {
                    BotComponentCache mate = teammates[j];
                    if (mate == null || mate.Bot == null || mate.Bot == _cache?.Bot)
                        continue;

                    mate.TacticalMemory?.SyncMemory(record.Position, "Echo:" + (_cache?.Bot?.Profile?.Id ?? "bot"));
                }
            }
        }

        #endregion

        #region Utility

        public void ResetMemory()
        {
            _enemyMemoryList.Clear();
            _enemyMemoryById.Clear();
            _clearedSpots.Clear();
        }

        public List<SeenEnemyRecord> GetAllMemory()
        {
            return _enemyMemoryList;
        }

        private static Vector3 SnapToGrid(Vector3 pos)
        {
            return new Vector3(
                Mathf.Round(pos.x / GridSnapSize) * GridSnapSize,
                Mathf.Round(pos.y / GridSnapSize) * GridSnapSize,
                Mathf.Round(pos.z / GridSnapSize) * GridSnapSize
            );
        }

        #endregion

        #region Types

        public struct SeenEnemyRecord
        {
            public Vector3 Position;
            public float TimeSeen;
            public string Tag;

            public SeenEnemyRecord(Vector3 position, float time, string tag)
            {
                Position = position;
                TimeSeen = time;
                Tag = tag;
            }
        }

        private class Vector3EqualityComparer : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b)
            {
                return (a - b).sqrMagnitude < PositionToleranceSqr;
            }

            public int GetHashCode(Vector3 v)
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + Mathf.RoundToInt(v.x * 10f);
                    hash = hash * 23 + Mathf.RoundToInt(v.y * 10f);
                    hash = hash * 23 + Mathf.RoundToInt(v.z * 10f);
                    return hash;
                }
            }
        }

        #endregion
    }
}
