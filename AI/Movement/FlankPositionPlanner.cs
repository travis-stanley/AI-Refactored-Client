#nullable enable

using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Calculates viable flank positions based on enemy direction and available cover space.
    /// </summary>
    public static class FlankPositionPlanner
    {
        private const float FlankOffset = 4.0f;
        private const float FlankRadius = 1.5f;
        private const float MaxFlankDistance = 12f;  // Maximum distance to consider for flank position

        public enum Side { Left, Right }

        /// <summary>
        /// Attempts to find a viable flank position based on enemy position and desired side.
        /// </summary>
        public static bool TryFindFlankPosition(Vector3 botPos, Vector3 enemyPos, out Vector3 flankPoint, Side preferred = Side.Left)
        {
            // Determine the direction to the enemy
            Vector3 direction = (enemyPos - botPos).normalized;

            // Calculate the perpendicular direction based on the preferred side
            Vector3 perpendicular = Quaternion.Euler(0f, preferred == Side.Left ? -90f : 90f, 0f) * direction;
            Vector3 candidate = botPos + perpendicular * FlankOffset;

            // Check if the calculated flank point is valid
            if (IsValidFlankPoint(candidate, botPos))
            {
                flankPoint = candidate;
                return true;
            }

            // Try the opposite side if the preferred side is invalid
            perpendicular = Quaternion.Euler(0f, preferred == Side.Left ? 90f : -90f, 0f) * direction;
            candidate = botPos + perpendicular * FlankOffset;

            if (IsValidFlankPoint(candidate, botPos))
            {
                flankPoint = candidate;
                return true;
            }

            // If no valid flank position found, return a zero vector
            flankPoint = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Validates if a potential flank position is suitable based on NavMesh and distance checks.
        /// </summary>
        private static bool IsValidFlankPoint(Vector3 point, Vector3 origin)
        {
            // Check if the point is valid on the NavMesh
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, FlankRadius, NavMesh.AllAreas))
            {
                float dist = Vector3.Distance(origin, hit.position);

                // Ensure the flank point is within an acceptable distance
                if (dist > 2f && dist < MaxFlankDistance)
                    return true;
            }

            return false;
        }
    }
}
