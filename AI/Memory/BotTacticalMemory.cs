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

        private readonly List<Vector3> _clearedLocations = new List<Vector3>(16);
        private readonly Dictionary<Vector3, float> _clearedTimestamps = new Dictionary<Vector3, float>(16);

        /// <summary>
        /// Returns true if the given location was recently cleared.
        /// </summary>
        public bool WasRecentlyCleared(Vector3 position)
        {
            float now = Time.time;
            bool recent = false;

            for (int i = _clearedLocations.Count - 1; i >= 0; i--)
            {
                Vector3 point = _clearedLocations[i];
                float timestamp;

                if (!_clearedTimestamps.TryGetValue(point, out timestamp) || now - timestamp > MemoryDuration)
                {
                    _clearedLocations.RemoveAt(i);
                    _clearedTimestamps.Remove(point);
                    continue;
                }

                if (!recent && Vector3.Distance(point, position) < ClearedThreshold)
                {
                    recent = true;
                }
            }

            return recent;
        }

        /// <summary>
        /// Marks a location as recently scanned or cleared.
        /// </summary>
        public void MarkCleared(Vector3 position)
        {
            float now = Time.time;

            for (int i = 0; i < _clearedLocations.Count; i++)
            {
                if (Vector3.Distance(_clearedLocations[i], position) < ClearedThreshold)
                {
                    _clearedTimestamps[_clearedLocations[i]] = now;
                    return;
                }
            }

            _clearedLocations.Add(position);
            _clearedTimestamps[position] = now;
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
