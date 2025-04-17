#nullable enable

using System;
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

        #region Location Memory

        private readonly List<Vector3> _clearedLocations = new();
        private readonly Dictionary<Vector3, float> _clearedTime = new();

        /// <summary>
        /// Returns true if the position has been recently cleared by the bot.
        /// </summary>
        public bool WasRecentlyCleared(Vector3 position)
        {
            float now = Time.time;
            bool foundRecent = false;

            for (int i = _clearedLocations.Count - 1; i >= 0; i--)
            {
                Vector3 point = _clearedLocations[i];
                float lastSeen = _clearedTime.TryGetValue(point, out var ts) ? ts : 0f;

                if (now - lastSeen > MemoryDuration)
                {
                    _clearedLocations.RemoveAt(i);
                    _clearedTime.Remove(point);
                    continue;
                }

                if (!foundRecent && Vector3.Distance(point, position) < ClearedThreshold)
                {
                    foundRecent = true;
                }
            }

            return foundRecent;
        }

        /// <summary>
        /// Marks a location as recently cleared or investigated.
        /// </summary>
        public void MarkCleared(Vector3 position)
        {
            float now = Time.time;

            for (int i = 0; i < _clearedLocations.Count; i++)
            {
                if (Vector3.Distance(_clearedLocations[i], position) < ClearedThreshold)
                {
                    _clearedTime[_clearedLocations[i]] = now;
                    return;
                }
            }

            _clearedLocations.Add(position);
            _clearedTime[position] = now;
        }

        #endregion

        #region Enemy Position Memory

        private Vector3? _lastKnownEnemy;
        private float _enemySeenTime = -999f;

        /// <summary>
        /// Records the last known position of an enemy for investigative use.
        /// </summary>
        public void RecordEnemyPosition(Vector3 position)
        {
            _lastKnownEnemy = position;
            _enemySeenTime = Time.time;
        }

        /// <summary>
        /// Returns the last seen enemy position if recent enough.
        /// </summary>
        public Vector3? GetLastKnownEnemyPosition()
        {
            return (_lastKnownEnemy.HasValue && Time.time - _enemySeenTime <= EnemyMemoryDuration)
                ? _lastKnownEnemy
                : null;
        }

        /// <summary>
        /// Clears stored enemy position memory.
        /// </summary>
        public void ClearLastKnownEnemy()
        {
            _lastKnownEnemy = null;
        }

        #endregion
    }
}
