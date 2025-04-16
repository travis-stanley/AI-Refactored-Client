#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility methods for calculating flashlight angle, direction, and exposure for bot perception.
    /// </summary>
    public static class FlashLightUtils
    {
        /// <summary>
        /// Determines if a light is currently within a blinding angle of the bot's head.
        /// </summary>
        public static bool IsBlindingLight(Transform lightTransform, Transform botHeadTransform, float angleThreshold = 30f)
        {
            if (lightTransform == null || botHeadTransform == null)
                return false;

            Vector3 directionToLight = (lightTransform.position - botHeadTransform.position).normalized;
            float angle = Vector3.Angle(botHeadTransform.forward, directionToLight);
            return angle < angleThreshold;
        }

        /// <summary>
        /// Calculates the relative intensity factor of a flashlight hitting the bot's face.
        /// </summary>
        public static float GetFlashIntensityFactor(Transform lightTransform, Transform botHeadTransform)
        {
            if (lightTransform == null || botHeadTransform == null)
                return 0f;

            Vector3 dirToLight = (lightTransform.position - botHeadTransform.position).normalized;
            return Mathf.Clamp01(Vector3.Dot(botHeadTransform.forward, dirToLight));
        }

        /// <summary>
        /// Checks if a light or actor is generally facing the target within a given angle threshold.
        /// </summary>
        public static bool IsFacingTarget(Transform source, Transform target, float angleThreshold = 30f)
        {
            if (source == null || target == null)
                return false;

            Vector3 directionToTarget = (target.position - source.position).normalized;
            float angle = Vector3.Angle(source.forward, directionToTarget);
            return angle < angleThreshold;
        }
    }
}
