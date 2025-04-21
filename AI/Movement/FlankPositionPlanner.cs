#nullable enable

using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Calculates viable flank positions based on enemy direction and available cover space.
    /// Ensures positions are realistic, NavMesh-valid, and within tactical distance constraints.
    /// </summary>
    public static class FlankPositionPlanner
    {
        #region Constants

        private const float FlankOffset = 4.0f;
        private const float FlankRadius = 1.5f;
        private const float MinFlankDistance = 2f;
        private const float MaxFlankDistance = 12f;

        #endregion

        #region Enums

        /// <summary>
        /// Sides relative to enemy direction.
        /// </summary>
        public enum Side
        {
            Left,
            Right
        }

        #endregion

        #region Public API

        /// <summary>
        /// Attempts to find a viable flank position based on enemy position and desired side.
        /// Tries preferred side first, then fallback to opposite if no valid point found.
        /// </summary>
        /// <param name="botPos">Bot’s current position.</param>
        /// <param name="enemyPos">Enemy position to flank around.</param>
        /// <param name="flankPoint">Resulting flank point if valid.</param>
        /// <param name="preferred">Preferred side to flank (left or right).</param>
        /// <returns>True if a valid flank point was found.</returns>
        public static bool TryFindFlankPosition(Vector3 botPos, Vector3 enemyPos, out Vector3 flankPoint, Side preferred = Side.Left)
        {
            Vector3 toEnemy = (enemyPos - botPos).normalized;

            // Reject if direction is invalid
            if (toEnemy.sqrMagnitude < 0.01f)
            {
                flankPoint = Vector3.zero;
                return false;
            }

            // Try preferred flank side
            if (TrySide(botPos, toEnemy, preferred, out flankPoint))
                return true;

            // Try opposite side if preferred fails
            Side opposite = preferred == Side.Left ? Side.Right : Side.Left;
            return TrySide(botPos, toEnemy, opposite, out flankPoint);
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Attempts to find a flank position on the specified side relative to enemy direction.
        /// </summary>
        private static bool TrySide(Vector3 origin, Vector3 directionToEnemy, Side side, out Vector3 result)
        {
            Vector3 perpendicular = Quaternion.Euler(0f, side == Side.Left ? -90f : 90f, 0f) * directionToEnemy;
            Vector3 candidate = origin + perpendicular * FlankOffset;

            if (IsValidFlankPoint(candidate, origin, out Vector3 adjusted))
            {
                result = adjusted;
                return true;
            }

            result = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Validates a flank candidate using NavMesh and distance.
        /// </summary>
        private static bool IsValidFlankPoint(Vector3 point, Vector3 origin, out Vector3 finalPosition)
        {
            finalPosition = Vector3.zero;

            if (NavMesh.SamplePosition(point, out NavMeshHit hit, FlankRadius, NavMesh.AllAreas))
            {
                float distSq = (origin - hit.position).sqrMagnitude;

                if (distSq >= MinFlankDistance * MinFlankDistance &&
                    distSq <= MaxFlankDistance * MaxFlankDistance)
                {
                    finalPosition = hit.position;
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
