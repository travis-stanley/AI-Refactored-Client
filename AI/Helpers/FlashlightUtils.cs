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
        /// Determines if a light is within a blinding angle of the bot's head orientation.
        /// </summary>
        public static bool IsBlindingLight(Transform lightTransform, Transform botHeadTransform, float angleThreshold = 30f)
        {
            if (lightTransform == null || botHeadTransform == null)
                return false;

            Vector3 toLight = lightTransform.position - botHeadTransform.position;
            float angle = Vector3.Angle(botHeadTransform.forward, toLight);
            return angle <= angleThreshold;
        }

        /// <summary>
        /// Calculates a normalized intensity factor based on direct exposure.
        /// Value approaches 1.0 when bot is looking directly into the light.
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
        /// Returns true if the source is facing the target within a given angle threshold.
        /// </summary>
        public static bool IsFacingTarget(Transform source, Transform target, float angleThreshold = 30f)
        {
            if (source == null || target == null)
                return false;

            Vector3 toTarget = target.position - source.position;
            float angle = Vector3.Angle(source.forward, toTarget);
            return angle <= angleThreshold;
        }

        #endregion

        #region Visibility Scoring

        /// <summary>
        /// Scores the visibility strength of a flashlight based on distance and angle.
        /// Used for flash reaction thresholding.
        /// </summary>
        public static float CalculateFlashScore(Transform lightTransform, Transform botHeadTransform, float maxDistance = 20f)
        {
            if (lightTransform == null || botHeadTransform == null)
                return 0f;

            Vector3 toLight = lightTransform.position - botHeadTransform.position;
            float distance = toLight.magnitude;

            // Angle factor based on the forward direction of the bot's head and light's position
            float angleFactor = Mathf.Clamp01(Vector3.Dot(botHeadTransform.forward, toLight.normalized));

            // Distance factor based on the max distance to the light
            float distanceFactor = 1f - Mathf.Clamp01(distance / maxDistance);

            // Final score is the product of the angle and distance factors
            return angleFactor * distanceFactor;
        }

        #endregion
    }
}
