#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Stores recent tactical memory for a bot, including last seen enemy positions and cleared scan points.
    /// Prevents redundant searching and improves squad efficiency.
    /// </summary>
    public class BotTacticalMemory : MonoBehaviour
    {
        private Vector3? _lastEnemyPosition;
        private float _lastEnemyTime;

        private const float MaxMemoryTime = 12f;
        private const float ClearRepeatDelay = 10f;

        private readonly Dictionary<Vector3, float> _clearedPositions = new(16);

        /// <summary>
        /// Remembers where an enemy was last seen.
        /// </summary>
        public void RecordEnemyPosition(Vector3 position)
        {
            _lastEnemyPosition = position;
            _lastEnemyTime = Time.time;
        }

        /// <summary>
        /// Gets the last known enemy position if still fresh.
        /// </summary>
        public Vector3? GetLastKnownEnemyPosition()
        {
            if (Time.time - _lastEnemyTime > MaxMemoryTime)
                return null;

            return _lastEnemyPosition;
        }

        /// <summary>
        /// Marks a location as cleared to avoid redundant searching.
        /// </summary>
        public void MarkCleared(Vector3 position)
        {
            _clearedPositions[position] = Time.time;
        }

        /// <summary>
        /// Checks whether a location was recently cleared.
        /// </summary>
        public bool WasRecentlyCleared(Vector3 position)
        {
            if (_clearedPositions.TryGetValue(position, out float clearedTime))
            {
                if (Time.time - clearedTime < ClearRepeatDelay)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Clears all memory (e.g. on respawn).
        /// </summary>
        public void ResetMemory()
        {
            _lastEnemyPosition = null;
            _lastEnemyTime = 0f;
            _clearedPositions.Clear();
        }
    }
}
