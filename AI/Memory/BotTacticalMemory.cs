#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Tracks tactical memory for a bot including last investigated locations and enemy sightings.
    /// Used to prevent redundant scans and support long-term awareness.
    /// </summary>
    public class BotTacticalMemory : MonoBehaviour
    {
        #region Constants

        private const float ClearedThreshold = 3.5f;
        private const float MemoryDuration = 60f;
        private const float EnemyMemoryDuration = 30f;

        #endregion

        #region Cleared Location Memory

        private readonly Dictionary<string, float> _clearedTimestamps = new Dictionary<string, float>(32);

        /// <summary>
        /// Returns true if the given location was recently cleared.
        /// </summary>
        public bool WasRecentlyCleared(Vector3 position)
        {
            string key = HashKey(position);
            float now = Time.time;

            if (_clearedTimestamps.TryGetValue(key, out float timestamp))
            {
                if (now - timestamp <= MemoryDuration)
                    return true;

                _clearedTimestamps.Remove(key);
            }

            return false;
        }

        /// <summary>
        /// Marks a location as recently scanned or cleared.
        /// </summary>
        public void MarkCleared(Vector3 position)
        {
            _clearedTimestamps[HashKey(position)] = Time.time;
        }

        private static string HashKey(Vector3 v)
        {
            // Round to reduce noise in memory footprint
            return $"{Mathf.Round(v.x * 0.5f) * 2f:F1}_{Mathf.Round(v.y):F1}_{Mathf.Round(v.z * 0.5f) * 2f:F1}";
        }

        #endregion

        #region Enemy Memory

        private Vector3? _lastKnownEnemyPos = null;
        private float _enemySeenTime = -999f;

        /// <summary>
        /// Stores the most recently seen enemy position.
        /// </summary>
        public void RecordEnemyPosition(Vector3 position)
        {
            _lastKnownEnemyPos = position;
            _enemySeenTime = Time.time;
        }

        /// <summary>
        /// Gets the most recent enemy position if memory is fresh.
        /// </summary>
        public Vector3? GetLastKnownEnemyPosition()
        {
            return (_lastKnownEnemyPos.HasValue && (Time.time - _enemySeenTime <= EnemyMemoryDuration))
                ? _lastKnownEnemyPos
                : null;
        }

        /// <summary>
        /// Clears stored enemy position data.
        /// </summary>
        public void ClearLastKnownEnemy()
        {
            _lastKnownEnemyPos = null;
        }

        #endregion
    }
}
