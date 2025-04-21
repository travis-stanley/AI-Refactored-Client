#nullable enable

using AIRefactored.AI.Core;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Stores recent enemy sightings, cleared points, and tactical decisions.
    /// Helps bots avoid redundant pathing and sync spotted positions with allies.
    /// </summary>
    public class BotTacticalMemory
    {
        #region Constants

        private const float MaxMemoryTime = 14f;
        private const float ClearedMemoryDuration = 10f;
        private const float PositionToleranceSqr = 0.25f;

        #endregion

        #region Fields

        private readonly Dictionary<Vector3, float> _clearedSpots = new(32, new Vector3EqualityComparer());
        private readonly List<SeenEnemyRecord> _enemyMemory = new(4);
        private BotComponentCache? _cache;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the tactical memory with bot cache reference.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
        }

        #endregion

        #region Enemy Tracking

        /// <summary>
        /// Records a seen enemy position.
        /// </summary>
        public void RecordEnemyPosition(Vector3 position, string tag = "Generic")
        {
            if (_cache == null || _cache.IsBlinded || (_cache.PanicHandler != null && _cache.PanicHandler.IsPanicking))
                return;

            float now = Time.time;
            Vector3 gridPos = SnapToGrid(position);

            for (int i = 0; i < _enemyMemory.Count; i++)
            {
                if ((gridPos - _enemyMemory[i].Position).sqrMagnitude < PositionToleranceSqr)
                {
                    _enemyMemory[i] = new SeenEnemyRecord(gridPos, now, tag);
                    return;
                }
            }

            _enemyMemory.Add(new SeenEnemyRecord(gridPos, now, tag));
        }

        /// <summary>
        /// Returns the freshest remembered enemy position.
        /// </summary>
        public Vector3? GetRecentEnemyMemory()
        {
            float now = Time.time;
            SeenEnemyRecord? freshest = null;

            for (int i = 0; i < _enemyMemory.Count; i++)
            {
                var mem = _enemyMemory[i];
                if (now - mem.TimeSeen <= MaxMemoryTime)
                {
                    if (freshest == null || mem.TimeSeen > freshest.Value.TimeSeen)
                        freshest = mem;
                }
            }

            return freshest?.Position;
        }

        /// <summary>
        /// Removes expired entries from memory.
        /// </summary>
        public void CullExpired()
        {
            float now = Time.time;
            _enemyMemory.RemoveAll(e => now - e.TimeSeen > MaxMemoryTime);
        }

        #endregion

        #region Cleared Locations

        /// <summary>
        /// Marks a search point as cleared.
        /// </summary>
        public void MarkCleared(Vector3 position)
        {
            Vector3 gridPos = SnapToGrid(position);
            _clearedSpots[gridPos] = Time.time;
        }

        /// <summary>
        /// Checks if a point was recently cleared.
        /// </summary>
        public bool WasRecentlyCleared(Vector3 position)
        {
            Vector3 gridPos = SnapToGrid(position);
            return _clearedSpots.TryGetValue(gridPos, out float lastTime) &&
                   Time.time - lastTime < ClearedMemoryDuration;
        }

        #endregion

        #region Squad Syncing

        /// <summary>
        /// Syncs an enemy memory entry from a squadmate.
        /// </summary>
        public void SyncMemory(Vector3 position, string tag = "AllyEcho")
        {
            RecordEnemyPosition(position, tag);
        }

        /// <summary>
        /// Shares tactical memory with squadmates.
        /// </summary>
        public void ShareMemoryWith(List<BotComponentCache> teammates)
        {
            for (int i = 0; i < _enemyMemory.Count; i++)
            {
                var entry = _enemyMemory[i];

                for (int j = 0; j < teammates.Count; j++)
                {
                    var mate = teammates[j];
                    if (mate?.Bot == null || mate.Bot == _cache?.Bot)
                        continue;

                    mate.TacticalMemory?.SyncMemory(entry.Position, $"Echo:{_cache?.Bot?.Profile?.Id ?? "bot"}");
                }
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Clears all tactical memory.
        /// </summary>
        public void ResetMemory()
        {
            _enemyMemory.Clear();
            _clearedSpots.Clear();
        }

        /// <summary>
        /// Returns a copy of enemy memory list.
        /// </summary>
        public List<SeenEnemyRecord> GetAllMemory() => _enemyMemory;

        /// <summary>
        /// Snaps vector to grid to improve positional comparisons.
        /// </summary>
        private static Vector3 SnapToGrid(Vector3 pos)
        {
            const float grid = 0.5f;
            return new Vector3(
                Mathf.Round(pos.x / grid) * grid,
                Mathf.Round(pos.y / grid) * grid,
                Mathf.Round(pos.z / grid) * grid
            );
        }

        #endregion

        #region Structures

        /// <summary>
        /// Tactical record of a seen enemy position.
        /// </summary>
        public struct SeenEnemyRecord
        {
            public Vector3 Position;
            public float TimeSeen;
            public string Tag;

            public SeenEnemyRecord(Vector3 pos, float time, string tag)
            {
                Position = pos;
                TimeSeen = time;
                Tag = tag;
            }
        }

        /// <summary>
        /// Grid-based comparer for Vector3 equality on memory keys.
        /// </summary>
        private class Vector3EqualityComparer : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b) =>
                (a - b).sqrMagnitude < PositionToleranceSqr;

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
