#nullable enable

using UnityEngine;
using UnityEngine.AI;
using EFT;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Calculates viable flank positions based on enemy direction and available cover space.
    /// </summary>
    public static class FlankPositionPlanner
    {
        private const float FlankOffset = 4.0f;
        private const float FlankRadius = 1.5f;

        public enum Side { Left, Right }

        public static bool TryFindFlankPosition(Vector3 botPos, Vector3 enemyPos, out Vector3 flankPoint, Side preferred = Side.Left)
        {
            Vector3 direction = (enemyPos - botPos).normalized;
            Vector3 perpendicular = Quaternion.Euler(0f, preferred == Side.Left ? -90f : 90f, 0f) * direction;
            Vector3 candidate = botPos + perpendicular * FlankOffset;

            if (IsValidFlankPoint(candidate, botPos))
            {
                flankPoint = candidate;
                return true;
            }

            // Try the opposite side
            perpendicular = Quaternion.Euler(0f, preferred == Side.Left ? 90f : -90f, 0f) * direction;
            candidate = botPos + perpendicular * FlankOffset;

            if (IsValidFlankPoint(candidate, botPos))
            {
                flankPoint = candidate;
                return true;
            }

            flankPoint = Vector3.zero;
            return false;
        }

        private static bool IsValidFlankPoint(Vector3 point, Vector3 origin)
        {
            if (NavMesh.SamplePosition(point, out NavMeshHit hit, FlankRadius, NavMesh.AllAreas))
            {
                float dist = Vector3.Distance(origin, hit.position);
                if (dist > 2f && dist < 12f)
                    return true;
            }

            return false;
        }
    }
}
