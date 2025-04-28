#nullable enable

namespace AIRefactored.AI.Navigation
{
    using UnityEngine;

    /// <summary>
    ///     Lightweight tactical navigation point metadata for fast lookup and AI decision-making.
    /// </summary>
    public sealed class NavPointData
    {
        public readonly float CoverAngle;

        public readonly float Elevation;

        public readonly string ElevationBand;

        public readonly bool IsCover;

        public readonly bool IsIndoor;

        public readonly bool IsJumpable;

        public readonly Vector3 Position;

        public readonly string Tag;

        public readonly string Zone;

        public NavPointData(
            Vector3 position,
            bool isCover,
            string tag,
            float elevation,
            bool isIndoor,
            bool isJumpable,
            float coverAngle,
            string zone,
            string elevationBand)
        {
            this.Position = position;
            this.IsCover = isCover;
            this.Tag = tag;
            this.Elevation = elevation;
            this.IsIndoor = isIndoor;
            this.IsJumpable = isJumpable;
            this.CoverAngle = coverAngle;
            this.Zone = zone;
            this.ElevationBand = elevationBand;
        }
    }
}