#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility class for determining properties of cover points using EFT's CustomNavigationPoint data.
    /// Supports cover evaluation for crouch, prone, and standing stances.
    /// </summary>
    public static class BotCoverHelper
    {
        /// <summary>
        /// Determines whether a cover object is considered "low" (requires crouch).
        /// </summary>
        public static bool IsLowCover(CustomNavigationPoint? cover)
        {
            return cover != null && cover.CoverLevel == CoverLevel.Sit;
        }

        /// <summary>
        /// Determines whether the cover is too low for crouch and may require prone.
        /// </summary>
        public static bool IsProneCover(CustomNavigationPoint? cover)
        {
            return cover != null && cover.CoverLevel == CoverLevel.Lay;
        }

        /// <summary>
        /// Determines whether the cover is tall enough for standing use.
        /// </summary>
        public static bool IsTallCover(CustomNavigationPoint? cover)
        {
            return cover != null && cover.CoverLevel == CoverLevel.Stay;
        }

        /// <summary>
        /// Optional raycast fallback: casts a vertical ray from cover point to estimate its height.
        /// Useful when CoverLevel is unknown or needs validation.
        /// </summary>
        public static float GetCoverSightHeight(CustomNavigationPoint? cover)
        {
            if (cover == null)
                return 0f;

            Vector3 top = cover.Position + Vector3.up * 2f;
            if (Physics.Raycast(top, Vector3.down, out RaycastHit hit, 2.5f))
            {
                return Vector3.Distance(top, hit.point);
            }

            return 0f;
        }

        /// <summary>
        /// Returns the stance recommendation for this cover: Standing, Crouch, or Prone.
        /// </summary>
        public static string GetRecommendedStance(CustomNavigationPoint? cover)
        {
            if (IsProneCover(cover)) return "Prone";
            if (IsLowCover(cover)) return "Crouch";
            if (IsTallCover(cover)) return "Stand";
            return "Unknown";
        }
    }
}
