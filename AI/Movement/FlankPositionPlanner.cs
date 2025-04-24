#nullable enable

using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Calculates realistic flank positions around enemies.
    /// Uses NavMesh validation and dynamic spacing based on enemy distance and vertical tolerance.
    /// </summary>
    public static class FlankPositionPlanner
    {
        #region Constants

        private const float BaseOffset = 4.0f;
        private const float OffsetVariation = 0.4f;
        private const float DistanceVariation = 1.5f;
        private const float NavSampleRadius = 1.5f;
        private const float VerticalTolerance = 1.85f;

        private const float MinDistance = 2f;
        private const float MaxDistance = 12f;

        private const int MaxAttemptsPerSide = 3;

        #endregion

        #region Enums

        public enum Side
        {
            Left,
            Right
        }

        #endregion

        #region Public API

        /// <summary>
        /// Attempts to find a valid flank point on a preferred side of the enemy.
        /// Falls back to the opposite side if preferred side fails.
        /// </summary>
        public static bool TryFindFlankPosition(Vector3 botPos, Vector3 enemyPos, out Vector3 flankPoint, Side preferred = Side.Left)
        {
            Vector3 toEnemy = enemyPos - botPos;
            flankPoint = Vector3.zero;

            if (toEnemy.sqrMagnitude < 0.01f)
                return false;

            toEnemy.y = 0f;
            toEnemy.Normalize();

            // Try preferred side
            if (TrySide(botPos, toEnemy, preferred, out flankPoint))
                return true;

            // Fallback to opposite side
            Side fallback = preferred == Side.Left ? Side.Right : Side.Left;
            return TrySide(botPos, toEnemy, fallback, out flankPoint);
        }

        /// <summary>
        /// Uses enemy forward vector and bot position to smartly pick a flank side.
        /// </summary>
        public static bool TrySmartFlank(Vector3 botPos, Vector3 enemyPos, Vector3 enemyForward, out Vector3 flankPoint)
        {
            Vector3 toBot = (botPos - enemyPos).normalized;
            float dot = Vector3.Dot(Vector3.Cross(enemyForward, Vector3.up), toBot);
            Side smartSide = dot >= 0f ? Side.Right : Side.Left;

            return TryFindFlankPosition(botPos, enemyPos, out flankPoint, smartSide);
        }

        #endregion

        #region Internal Logic

        private static bool TrySide(Vector3 origin, Vector3 toEnemy, Side side, out Vector3 result)
        {
            result = Vector3.zero;
            Vector3 perpendicular = Vector3.Cross(Vector3.up, toEnemy) * (side == Side.Left ? -1f : 1f);

            for (int i = 0; i < MaxAttemptsPerSide; i++)
            {
                float offset = BaseOffset + Random.Range(-OffsetVariation, OffsetVariation);
                float distance = Random.Range(MinDistance, MaxDistance);

                Vector3 candidate = origin + perpendicular * offset + toEnemy * distance;

                if (IsValidFlankPoint(candidate, origin, out Vector3 validated))
                {
                    result = validated;
                    return true;
                }
            }

            return false;
        }

        private static bool IsValidFlankPoint(Vector3 candidate, Vector3 origin, out Vector3 final)
        {
            final = Vector3.zero;

            if (!NavMesh.SamplePosition(candidate, out var hit, NavSampleRadius, NavMesh.AllAreas))
                return false;

            float verticalDelta = Mathf.Abs(origin.y - hit.position.y);
            float sqrDist = (origin - hit.position).sqrMagnitude;

            if (sqrDist < MinDistance * MinDistance || sqrDist > MaxDistance * MaxDistance)
                return false;

            if (verticalDelta > VerticalTolerance)
                return false;

            final = hit.position;
            return true;
        }

        #endregion
    }
}
