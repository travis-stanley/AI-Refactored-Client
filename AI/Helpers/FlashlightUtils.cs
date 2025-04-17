#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility methods for evaluating flashlight visibility, exposure angles, and intensity effects.
    /// Used for bot perception logic related to visual overload and flashblindness.
    /// </summary>
    public static class FlashLightUtils
    {
        #region Exposure Checks

        /// <summary>
        /// Determines if a light is positioned within a blinding angle of the bot's head direction.
        /// </summary>
        /// <param name="lightTransform">The light's transform in world space.</param>
        /// <param name="botHeadTransform">The transform representing the bot's head orientation.</param>
        /// <param name="angleThreshold">Maximum angle (in degrees) to be considered blinding.</param>
        public static bool IsBlindingLight(Transform lightTransform, Transform botHeadTransform, float angleThreshold = 30f)
        {
            if (lightTransform == null || botHeadTransform == null)
                return false;

            Vector3 directionToLight = lightTransform.position - botHeadTransform.position;
            float angle = Vector3.Angle(botHeadTransform.forward, directionToLight);
            return angle < angleThreshold;
        }

        /// <summary>
        /// Calculates the forward-facing intensity factor of a flashlight.
        /// Value is 1.0 when directly in front, 0.0 when behind.
        /// </summary>
        public static float GetFlashIntensityFactor(Transform lightTransform, Transform botHeadTransform)
        {
            if (lightTransform == null || botHeadTransform == null)
                return 0f;

            Vector3 toLight = lightTransform.position - botHeadTransform.position;
            float dot = Vector3.Dot(botHeadTransform.forward, toLight.normalized);
            return Mathf.Clamp01(dot);
        }

        #endregion

        #region Alignment Checks

        /// <summary>
        /// Determines whether a source is facing toward a target within a given angle threshold.
        /// </summary>
        public static bool IsFacingTarget(Transform source, Transform target, float angleThreshold = 30f)
        {
            if (source == null || target == null)
                return false;

            Vector3 toTarget = target.position - source.position;
            float angle = Vector3.Angle(source.forward, toTarget);
            return angle < angleThreshold;
        }

        #endregion

        #region Visibility Scoring

        /// <summary>
        /// Calculates a visibility score based on angle and distance.
        /// Used to determine flash effectiveness in bot vision systems.
        /// </summary>
        /// <param name="lightTransform">The light source transform.</param>
        /// <param name="botHeadTransform">The bot head transform.</param>
        /// <param name="maxDistance">Maximum effective range for flash effect falloff.</param>
        public static float CalculateFlashScore(Transform lightTransform, Transform botHeadTransform, float maxDistance = 20f)
        {
            if (lightTransform == null || botHeadTransform == null)
                return 0f;

            Vector3 toLight = lightTransform.position - botHeadTransform.position;
            float distance = toLight.magnitude;
            float dot = Vector3.Dot(botHeadTransform.forward, toLight.normalized);

            float angleScore = Mathf.Clamp01(dot);
            float distanceFalloff = 1f - Mathf.Clamp01(distance / maxDistance);

            return angleScore * distanceFalloff;
        }

        #endregion
    }
}
