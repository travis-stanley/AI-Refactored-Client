#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Navigation;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility for scoring, checking, and tracking tactical cover positions.
    /// Supports memory cooldown, physics checks, and realism-tuned exposure logic.
    /// </summary>
    public static class BotCoverHelper
    {
        #region Cover Memory

        private static readonly Dictionary<string, float> CoverMemory = new(128);
        private const float MemoryDuration = 20f;

        public static void MarkUsed(Vector3 position)
        {
            CoverMemory[GetKey(position)] = Time.time;
        }

        public static bool WasRecentlyUsed(Vector3 position)
        {
            return CoverMemory.TryGetValue(GetKey(position), out float last) && (Time.time - last) < MemoryDuration;
        }

        private static string GetKey(Vector3 pos)
        {
            return $"{Mathf.RoundToInt(pos.x)}_{Mathf.RoundToInt(pos.y)}_{Mathf.RoundToInt(pos.z)}";
        }

        #endregion

        #region World-Space Cover Detection

        public static bool IsLowCover(Vector3 position)
        {
            Vector3 eye = position + Vector3.up * 1.25f;
            return RayHitsSolid(eye, Vector3.forward, 1.5f);
        }

        public static bool IsProneCover(Vector3 position)
        {
            Vector3 eye = position + Vector3.up * 1.25f;
            return RayHitsSolid(eye, -Vector3.forward, 1.5f);
        }

        public static bool IsStandingCover(Vector3 position)
        {
            Vector3 eye = position + Vector3.up * 1.6f;
            return RayHitsSolid(eye, Vector3.forward, 2.25f);
        }

        private static bool RayHitsSolid(Vector3 origin, Vector3 dir, float dist)
        {
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, dist))
                return false;

            return IsSolid(hit.collider);
        }

        private static bool IsSolid(Collider? collider)
        {
            if (collider == null)
                return false;

            string tag = collider.tag.ToLowerInvariant();
            string mat = collider.sharedMaterial != null ? collider.sharedMaterial.name.ToLowerInvariant() : "";

            return !(tag.Contains("glass") ||
                     tag.Contains("foliage") ||
                     tag.Contains("banner") ||
                     mat.Contains("leaf") ||
                     mat.Contains("bush") ||
                     mat.Contains("net") ||
                     mat.Contains("fabric"));
        }

        #endregion

        #region Cover Stance Setter

        /// <summary>
        /// Attempts to set crouch or prone based on nearby cover types near the given position.
        /// </summary>
        public static void TrySetStanceFromNearbyCover(BotComponentCache cache, Vector3 position)
        {
            var pose = cache.PoseController;
            if (pose == null)
                return;

            List<Vector3> points = NavPointRegistry.QueryNearby(position, 4f, point =>
            {
                float distSq = (point - position).sqrMagnitude;
                return distSq <= 16f && (
                    IsProneCover(point) ||
                    IsLowCover(point)
                );
            });

            foreach (var point in points)
            {
                if (IsProneCover(point))
                {
                    pose.SetProne(true);
                    break;
                }
                else if (IsLowCover(point))
                {
                    pose.SetCrouch(true);
                    break;
                }
            }
        }

        #endregion

        #region Scoring (Vector3)

        public static float Score(Vector3 point, Vector3 botPos, Vector3 threatPos)
        {
            float distToBot = Vector3.Distance(botPos, point);
            float distToThreat = Vector3.Distance(threatPos, point);
            float angle = Vector3.Angle(threatPos - point, botPos - point);

            float typeBonus = 0.5f;
            if (IsProneCover(point)) typeBonus = 1.25f;
            else if (IsLowCover(point)) typeBonus = 1.0f;
            else if (IsStandingCover(point)) typeBonus = 0.85f;

            float threatFactor = Mathf.Clamp01(distToThreat / 20f);
            float angleFactor = Mathf.Clamp01(angle / 180f);

            float rawScore = typeBonus + threatFactor + angleFactor;
            return rawScore / (1f + distToBot * 0.15f);
        }

        #endregion

        #region Cover API for NavPoint Registry

        public static bool IsLowCover(CustomNavigationPoint? point)
        {
            return point != null && point.CoverLevel == CoverLevel.Sit;
        }

        public static bool IsProneCover(CustomNavigationPoint? point)
        {
            return point != null && point.CoverLevel == CoverLevel.Lay;
        }

        public static bool IsStandingCover(CustomNavigationPoint? point)
        {
            return point != null && point.CoverLevel == CoverLevel.Stay;
        }

        public static void MarkUsed(CustomNavigationPoint? point)
        {
            if (point != null)
                MarkUsed(point.Position);
        }

        public static bool WasRecentlyUsed(CustomNavigationPoint? point)
        {
            return point != null && WasRecentlyUsed(point.Position);
        }

        public static float Score(CustomNavigationPoint point, Vector3 botPos, Vector3 threatPos)
        {
            Vector3 p = point.Position;

            float distToBot = Vector3.Distance(botPos, p);
            float distToThreat = Vector3.Distance(threatPos, p);
            float angle = Vector3.Angle(threatPos - p, botPos - p);

            float typeBonus = 0.5f;
            if (IsProneCover(point)) typeBonus = 1.25f;
            else if (IsLowCover(point)) typeBonus = 1.0f;
            else if (IsStandingCover(point)) typeBonus = 0.85f;

            float threatFactor = Mathf.Clamp01(distToThreat / 20f);
            float angleFactor = Mathf.Clamp01(angle / 180f);

            float rawScore = typeBonus + threatFactor + angleFactor;
            return rawScore / (1f + distToBot * 0.15f);
        }

        #endregion
    }
}
