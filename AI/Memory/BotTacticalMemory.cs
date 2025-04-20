#nullable enable

using AIRefactored.AI.Core;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Stores recent tactical memory for a bot, including last seen enemy positions, scan points, and per-bone visibility.
    /// Prevents redundant investigation and encourages coordinated squad behavior.
    /// </summary>
    public class BotTacticalMemory
    {
        private const float MaxMemoryTime = 14f;
        private const float ClearedMemoryDuration = 10f;
        private const float PositionToleranceSqr = 0.5f * 0.5f;

        private readonly Dictionary<Vector3, float> _clearedSpots = new(32, new Vector3EqualityComparer());
        private readonly List<SeenEnemyRecord> _enemyMemory = new(4);

        private BotComponentCache? _cache;

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
        }

        /// <summary>
        /// Adds a new enemy position to memory.
        /// </summary>
        public void RecordEnemyPosition(Vector3 position, string tag = "Generic")
        {
            float now = Time.time;
            Vector3 gridPos = SnapToGrid(position);

            for (int i = 0; i < _enemyMemory.Count; i++)
            {
                if ((gridPos - _enemyMemory[i].Position).sqrMagnitude < PositionToleranceSqr)
                {
                    // Refresh existing memory
                    _enemyMemory[i] = new SeenEnemyRecord(gridPos, now, tag);
                    return;
                }
            }

            _enemyMemory.Add(new SeenEnemyRecord(gridPos, now, tag));
        }

        /// <summary>
        /// Returns the most recent valid enemy position if one exists.
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
        /// Clears outdated or old enemy memory.
        /// </summary>
        public void CullExpired()
        {
            float now = Time.time;
            _enemyMemory.RemoveAll(e => now - e.TimeSeen > MaxMemoryTime);
        }

        /// <summary>
        /// Marks a search location as cleared.
        /// </summary>
        public void MarkCleared(Vector3 position)
        {
            Vector3 gridPos = SnapToGrid(position);
            _clearedSpots[gridPos] = Time.time;
        }

        /// <summary>
        /// Checks if a location was recently cleared.
        /// </summary>
        public bool WasRecentlyCleared(Vector3 position)
        {
            Vector3 gridPos = SnapToGrid(position);
            return _clearedSpots.TryGetValue(gridPos, out float lastCleared) &&
                   Time.time - lastCleared < ClearedMemoryDuration;
        }

        /// <summary>
        /// Removes all stored data.
        /// </summary>
        public void ResetMemory()
        {
            _enemyMemory.Clear();
            _clearedSpots.Clear();
        }

        /// <summary>
        /// Returns all memory entries (for debug or sync).
        /// </summary>
        public List<SeenEnemyRecord> GetAllMemory()
        {
            return _enemyMemory;
        }

        /// <summary>
        /// Allows other bots to sync memory (e.g., squadmate spotted enemy).
        /// </summary>
        public void SyncMemory(Vector3 position, string tag = "AllyEcho")
        {
            RecordEnemyPosition(position, tag);
        }

        private static Vector3 SnapToGrid(Vector3 pos)
        {
            const float gridSize = 0.5f;
            return new Vector3(
                Mathf.Round(pos.x / gridSize) * gridSize,
                Mathf.Round(pos.y / gridSize) * gridSize,
                Mathf.Round(pos.z / gridSize) * gridSize
            );
        }

        /// <summary>
        /// Lightweight record of a spotted enemy position and its source.
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
        /// Fast grid-based equality comparer for Vector3.
        /// </summary>
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
    }
}
