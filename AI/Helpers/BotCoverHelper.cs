#nullable enable

namespace AIRefactored.AI.Helpers
{
    using System.Collections.Generic;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Navigation;

    using UnityEngine;

    /// <summary>
    ///     Utility for scoring, checking, and tracking tactical cover positions.
    ///     Supports memory cooldown, physics checks, and realism-tuned exposure logic.
    /// </summary>
    public static class BotCoverHelper
    {
        private const float MemoryDuration = 20f;

        private static readonly Dictionary<string, float> CoverMemory = new(128);

        public static bool IsLowCover(Vector3 position)
        {
            var eye = position + Vector3.up * 1.25f;
            return RayHitsSolid(eye, Vector3.forward, 1.5f);
        }

        public static bool IsLowCover(CustomNavigationPoint? point)
        {
            return point != null && point.CoverLevel == CoverLevel.Sit;
        }

        public static bool IsProneCover(Vector3 position)
        {
            var eye = position + Vector3.up * 1.25f;
            return RayHitsSolid(eye, -Vector3.forward, 1.5f);
        }

        public static bool IsProneCover(CustomNavigationPoint? point)
        {
            return point != null && point.CoverLevel == CoverLevel.Lay;
        }

        public static bool IsStandingCover(Vector3 position)
        {
            var eye = position + Vector3.up * 1.6f;
            return RayHitsSolid(eye, Vector3.forward, 2.25f);
        }

        public static bool IsStandingCover(CustomNavigationPoint? point)
        {
            return point != null && point.CoverLevel == CoverLevel.Stay;
        }

        public static void MarkUsed(Vector3 position)
        {
            CoverMemory[GetKey(position)] = Time.time;
        }

        public static void MarkUsed(CustomNavigationPoint? point)
        {
            if (point != null)
                MarkUsed(point.Position);
        }

        public static float Score(Vector3 point, Vector3 botPos, Vector3 threatPos)
        {
            var distToBot = Vector3.Distance(botPos, point);
            var distToThreat = Vector3.Distance(threatPos, point);
            var angle = Vector3.Angle(threatPos - point, botPos - point);

            var typeBonus = 0.5f;
            if (IsProneCover(point)) typeBonus = 1.25f;
            else if (IsLowCover(point)) typeBonus = 1.0f;
            else if (IsStandingCover(point)) typeBonus = 0.85f;

            var threatFactor = Mathf.Clamp01(distToThreat / 20f);
            var angleFactor = Mathf.Clamp01(angle / 180f);

            var rawScore = typeBonus + threatFactor + angleFactor;
            return rawScore / (1f + distToBot * 0.15f);
        }

        public static float Score(CustomNavigationPoint point, Vector3 botPos, Vector3 threatPos)
        {
            var p = point.Position;
            var distToBot = Vector3.Distance(botPos, p);
            var distToThreat = Vector3.Distance(threatPos, p);
            var angle = Vector3.Angle(threatPos - p, botPos - p);

            var typeBonus = 0.5f;
            if (IsProneCover(point)) typeBonus = 1.25f;
            else if (IsLowCover(point)) typeBonus = 1.0f;
            else if (IsStandingCover(point)) typeBonus = 0.85f;

            var threatFactor = Mathf.Clamp01(distToThreat / 20f);
            var angleFactor = Mathf.Clamp01(angle / 180f);

            var rawScore = typeBonus + threatFactor + angleFactor;
            return rawScore / (1f + distToBot * 0.15f);
        }

        /// <summary>
        ///     Attempts to set crouch or prone based on nearby cover types near the given position.
        /// </summary>
        public static void TrySetStanceFromNearbyCover(BotComponentCache cache, Vector3 position)
        {
            var pose = cache.PoseController;
            if (pose == null) return;

            var points = NavPointRegistry.QueryNearby(
                position,
                4f,
                (Vector3 point) =>
                    {
                        var distSq = (point - position).sqrMagnitude;
                        return distSq <= 16f && (IsProneCover(point) || IsLowCover(point));
                    });

            if (points.Count == 0)
                return;

            foreach (var point in points)
            {
                if (IsProneCover(point))
                {
                    pose.SetProne(true);
                    break;
                }

                if (IsLowCover(point))
                {
                    pose.SetCrouch(true);
                    break;
                }
            }
        }

        public static bool WasRecentlyUsed(Vector3 position)
        {
            return CoverMemory.TryGetValue(GetKey(position), out var last) && Time.time - last < MemoryDuration;
        }

        public static bool WasRecentlyUsed(CustomNavigationPoint? point)
        {
            return point != null && WasRecentlyUsed(point.Position);
        }

        private static string GetKey(Vector3 pos)
        {
            return $"{Mathf.RoundToInt(pos.x)}_{Mathf.RoundToInt(pos.y)}_{Mathf.RoundToInt(pos.z)}";
        }

        private static bool IsSolid(Collider? collider)
        {
            if (collider == null)
                return false;

            var tag = collider.tag?.ToLowerInvariant() ?? string.Empty;
            var mat = collider.sharedMaterial?.name?.ToLowerInvariant() ?? string.Empty;

            return !(tag.Contains("glass") || tag.Contains("foliage") || tag.Contains("banner") || mat.Contains("leaf")
                     || mat.Contains("bush") || mat.Contains("net") || mat.Contains("fabric"));
        }

        private static bool RayHitsSolid(Vector3 origin, Vector3 dir, float dist)
        {
            if (!Physics.Raycast(origin, dir, out var hit, dist))
                return false;

            return IsSolid(hit.collider);
        }
    }
}