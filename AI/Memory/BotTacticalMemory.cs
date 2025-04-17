#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Tracks tactical memory for a bot including last investigated locations and enemy sightings.
    /// Used to prevent redundant checks and support long-term awareness.
    /// </summary>
    public class BotTacticalMemory : MonoBehaviour
    {
        #region Investigated Locations

        private readonly List<Vector3> _clearedLocations = new List<Vector3>();
        private const float ClearedThreshold = 3.5f;
        private const float MemoryDuration = 60f;

        private readonly Dictionary<Vector3, float> _clearedTime = new Dictionary<Vector3, float>();

        /// <summary>
        /// Returns true if a location has already been investigated recently.
        /// </summary>
        public bool WasRecentlyCleared(Vector3 position)
        {
            float now = Time.time;
            for (int i = _clearedLocations.Count - 1; i >= 0; i--)
            {
                Vector3 point = _clearedLocations[i];
                if (Vector3.Distance(point, position) < ClearedThreshold)
                {
                    float lastSeen;
                    if (_clearedTime.TryGetValue(point, out lastSeen))
                    {
                        if (now - lastSeen < MemoryDuration)
                            return true;
                        else
                        {
                            _clearedLocations.RemoveAt(i);
                            _clearedTime.Remove(point);
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Marks a location as recently investigated.
        /// </summary>
        public void MarkCleared(Vector3 position)
        {
            float now = Time.time;
            _clearedLocations.Add(position);
            _clearedTime[position] = now;
        }

        #endregion

        #region Last Known Enemy Position

        private Vector3? _lastKnownEnemy;
        private float _enemySeenTime;

        public void RecordEnemyPosition(Vector3 pos)
        {
            _lastKnownEnemy = pos;
            _enemySeenTime = Time.time;
        }

        public Vector3? GetLastKnownEnemyPosition()
        {
            if (_lastKnownEnemy.HasValue && Time.time - _enemySeenTime < 30f)
                return _lastKnownEnemy;

            return null;
        }

        public void ClearLastKnownEnemy()
        {
            _lastKnownEnemy = null;
        }

        #endregion
    }
}
