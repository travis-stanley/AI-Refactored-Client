#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility class for determining properties of cover points using EFT's CustomNavigationPoint data.
    /// </summary>
    public static class BotCoverHelper
    {
        /// <summary>
        /// Determines whether a cover object is considered "low" (requires crouch).
        /// </summary>
        public static bool IsLowCover(CustomNavigationPoint cover)
        {
            return cover.CoverLevel == CoverLevel.Sit;
        }

        /// <summary>
        /// Determines whether the cover is too low for crouch and may require prone.
        /// </summary>
        public static bool IsProneCover(CustomNavigationPoint cover)
        {
            return cover.CoverLevel == CoverLevel.Lay;
        }

        /// <summary>
        /// Determines whether the cover is tall enough for standing use.
        /// </summary>
        public static bool IsTallCover(CustomNavigationPoint cover)
        {
            return cover.CoverLevel == CoverLevel.Stay;
        }

        /// <summary>
        /// Optional raycast fallback: casts a vertical ray to measure sight occlusion height.
        /// </summary>
        public static float GetCoverSightHeight(CustomNavigationPoint cover)
        {
            Vector3 top = cover.Position + Vector3.up * 2f;
            if (Physics.Raycast(top, Vector3.down, out RaycastHit hit, 2.5f))
            {
                return Vector3.Distance(top, hit.point);
            }

            return 0f;
        }
    }
}
