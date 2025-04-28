#nullable enable

namespace AIRefactored.AI.Movement
{
    using UnityEngine;
    using UnityEngine.AI;

    /// <summary>
    ///     Calculates realistic flank positions around enemies.
    ///     Uses NavMesh validation and dynamic spacing based on enemy distance and vertical tolerance.
    /// </summary>
    public static class FlankPositionPlanner
    {
        private const float BaseOffset = 4.0f;

        private const float DistanceVariation = 1.5f;

        private const int MaxAttemptsPerSide = 3;

        private const float MaxDistance = 12f;

        private const float MinDistance = 2f;

        private const float NavSampleRadius = 1.5f;

        private const float OffsetVariation = 0.4f;

        private const float VerticalTolerance = 1.85f;

        public enum Side
        {
            Left,

            Right
        }

        /// <summary>
        ///     Attempts to find a valid flank point on a preferred side of the enemy.
        ///     Falls back to the opposite side if preferred side fails.
        /// </summary>
        public static bool TryFindFlankPosition(
            Vector3 botPos,
            Vector3 enemyPos,
            out Vector3 flankPoint,
            Side preferred = Side.Left)
        {
            flankPoint = Vector3.zero;
            var toEnemy = enemyPos - botPos;

            if (toEnemy.sqrMagnitude < 0.01f)
                return false;

            toEnemy.y = 0f;
            toEnemy.Normalize();

            // Try preferred side first
            if (TrySide(botPos, toEnemy, preferred, out flankPoint))
                return true;

            // Fallback to opposite side
            var fallback = preferred == Side.Left ? Side.Right : Side.Left;
            return TrySide(botPos, toEnemy, fallback, out flankPoint);
        }

        /// <summary>
        ///     Uses enemy forward vector and bot position to smartly pick a flank side.
        /// </summary>
        public static bool TrySmartFlank(Vector3 botPos, Vector3 enemyPos, Vector3 enemyForward, out Vector3 flankPoint)
        {
            var toBot = (botPos - enemyPos).normalized;
            var dot = Vector3.Dot(Vector3.Cross(enemyForward, Vector3.up), toBot);
            var smartSide = dot >= 0f ? Side.Right : Side.Left;

            return TryFindFlankPosition(botPos, enemyPos, out flankPoint, smartSide);
        }

        private static bool IsValidFlankPoint(Vector3 candidate, Vector3 origin, out Vector3 final)
        {
            final = Vector3.zero;

            if (!NavMesh.SamplePosition(candidate, out var hit, NavSampleRadius, NavMesh.AllAreas))
                return false;

            var verticalDelta = Mathf.Abs(origin.y - hit.position.y);
            var sqrDist = (origin - hit.position).sqrMagnitude;

            if (sqrDist < MinDistance * MinDistance || sqrDist > MaxDistance * MaxDistance)
                return false;

            if (verticalDelta > VerticalTolerance)
                return false;

            final = hit.position;
            return true;
        }

        private static bool TrySide(Vector3 origin, Vector3 toEnemy, Side side, out Vector3 result)
        {
            result = Vector3.zero;
            var perpendicular = Vector3.Cross(Vector3.up, toEnemy) * (side == Side.Left ? -1f : 1f);

            for (var i = 0; i < MaxAttemptsPerSide; i++)
            {
                var offset = BaseOffset + Random.Range(-OffsetVariation, OffsetVariation);
                var distance = Random.Range(MinDistance, MaxDistance);

                var candidate = origin + perpendicular * offset + toEnemy * distance;

                if (IsValidFlankPoint(candidate, origin, out var validated))
                {
                    result = validated;
                    return true;
                }
            }

            return false;
        }
    }
}