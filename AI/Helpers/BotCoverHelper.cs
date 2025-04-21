#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility class for evaluating cover point properties using EFT's CustomNavigationPoint data.
    /// Supports cover classification for tactical stance decisions (prone, crouch, stand).
    /// </summary>
    public static class BotCoverHelper
    {
        #region Cover Type Checks

        /// <summary>
        /// Determines whether a cover object is considered low (requires crouching).
        /// </summary>
        /// <param name="cover">The cover navigation point to evaluate.</param>
        /// <returns>True if cover is crouch-height.</returns>
        public static bool IsLowCover(CustomNavigationPoint? cover)
        {
            return cover != null && cover.CoverLevel == CoverLevel.Sit;
        }

        /// <summary>
        /// Determines whether a cover object is prone-height (too low for crouch).
        /// </summary>
        /// <param name="cover">The cover navigation point to evaluate.</param>
        /// <returns>True if prone is required.</returns>
        public static bool IsProneCover(CustomNavigationPoint? cover)
        {
            return cover != null && cover.CoverLevel == CoverLevel.Lay;
        }

        /// <summary>
        /// Determines whether the cover is tall enough for standing usage.
        /// </summary>
        /// <param name="cover">The cover navigation point to evaluate.</param>
        /// <returns>True if standing cover.</returns>
        public static bool IsTallCover(CustomNavigationPoint? cover)
        {
            return cover != null && cover.CoverLevel == CoverLevel.Stay;
        }

        #endregion

        #region Cover Fallback / Raycast Logic

        /// <summary>
        /// Performs a downward raycast from above the cover point to estimate surface height.
        /// Can be used when CoverLevel is unknown or needs runtime validation.
        /// </summary>
        /// <param name="cover">The cover point to scan downward from.</param>
        /// <returns>Estimated height of cover relative to player eye level.</returns>
        public static float GetCoverSightHeight(CustomNavigationPoint? cover)
        {
            if (cover == null)
                return 0f;

            Vector3 origin = cover.Position + Vector3.up * 2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 2.5f, Physics.DefaultRaycastLayers))
            {
                return Vector3.Distance(origin, hit.point);
            }

            return 0f;
        }

        #endregion

        #region Pose Suggestion

        /// <summary>
        /// Suggests an optimal pose (Stand, Crouch, Prone) based on the cover object.
        /// </summary>
        /// <param name="cover">Cover point to evaluate.</param>
        /// <returns>Recommended pose type based on geometry and level.</returns>
        public static PoseType GetRecommendedPose(CustomNavigationPoint? cover)
        {
            if (IsProneCover(cover)) return PoseType.Prone;
            if (IsLowCover(cover)) return PoseType.Crouch;
            if (IsTallCover(cover)) return PoseType.Stand;
            return PoseType.Unknown;
        }

        #endregion
    }

    /// <summary>
    /// Stance type recommendation used by bots for choosing posture near cover.
    /// </summary>
    public enum PoseType
    {
        /// <summary>
        /// No recommendation or invalid input.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// Fully upright stance.
        /// </summary>
        Stand = 1,

        /// <summary>
        /// Crouching stance, suitable for low cover.
        /// </summary>
        Crouch = 2,

        /// <summary>
        /// Prone stance, used for extremely low or ground-level concealment.
        /// </summary>
        Prone = 3
    }
}
