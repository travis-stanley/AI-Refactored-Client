#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Evaluates directional exposure to intense lights such as flashlights and flares.
    /// Used by bot perception systems to simulate flashblindness, directional exposure, and evasive behavior.
    /// </summary>
    public static class FlashLightUtils
    {
        #region Exposure Checks

        /// <summary>
        /// Returns true if the bot is looking directly at the light source within a blinding cone angle.
        /// </summary>
        /// <param name="lightTransform">Transform of the flashlight or light-emitting object.</param>
        /// <param name="botHeadTransform">Transform representing the bot's eye or head.</param>
        /// <param name="angleThreshold">Maximum angle in degrees to consider the light blinding.</param>
        /// <returns>True if the bot is within the blinding cone.</returns>
        public static bool IsBlindingLight(Transform lightTransform, Transform botHeadTransform, float angleThreshold = 30f)
        {
            if (lightTransform == null || botHeadTransform == null)
                return false;

            Vector3 toLight = lightTransform.position - botHeadTransform.position;
            float angle = Vector3.Angle(botHeadTransform.forward, toLight);
            return angle <= angleThreshold;
        }

        /// <summary>
        /// Computes a normalized value representing how directly the bot is facing the light.
        /// </summary>
        /// <param name="lightTransform">Transform of the light source.</param>
        /// <param name="botHeadTransform">Transform of the bot's head or eyes.</param>
        /// <returns>Flash intensity factor from 0.0 (no effect) to 1.0 (full exposure).</returns>
        public static float GetFlashIntensityFactor(Transform lightTransform, Transform botHeadTransform)
        {
            if (lightTransform == null || botHeadTransform == null)
                return 0f;

            Vector3 toLight = lightTransform.position - botHeadTransform.position;
            float dot = Vector3.Dot(botHeadTransform.forward, toLight.normalized);
            return Mathf.Clamp01(dot);
        }

        #endregion

        #region Orientation Checks

        /// <summary>
        /// Determines whether the light source is facing toward the bot within a directional tolerance.
        /// </summary>
        /// <param name="source">Transform of the light-emitting object.</param>
        /// <param name="target">Transform of the bot or receiver.</param>
        /// <param name="angleThreshold">Threshold angle in degrees for directional alignment.</param>
        /// <returns>True if the source is facing the target.</returns>
        public static bool IsFacingTarget(Transform source, Transform target, float angleThreshold = 30f)
        {
            if (source == null || target == null)
                return false;

            Vector3 toTarget = target.position - source.position;
            float angle = Vector3.Angle(source.forward, toTarget);
            return angle <= angleThreshold;
        }

        #endregion

        #region Visibility Score

        /// <summary>
        /// Calculates a flash score based on distance and alignment.
        /// 1.0 = direct exposure at close range, 0.0 = no meaningful exposure.
        /// </summary>
        /// <param name="lightTransform">The flashlight or flare transform.</param>
        /// <param name="botHeadTransform">The bot's head transform.</param>
        /// <param name="maxDistance">Maximum range in meters for full flash effect.</param>
        /// <returns>Flash score between 0.0 and 1.0.</returns>
        public static float CalculateFlashScore(Transform lightTransform, Transform botHeadTransform, float maxDistance = 20f)
        {
            if (lightTransform == null || botHeadTransform == null)
                return 0f;

            Vector3 toLight = lightTransform.position - botHeadTransform.position;
            float distance = toLight.magnitude;

            float angleScore = Mathf.Clamp01(Vector3.Dot(botHeadTransform.forward, toLight.normalized));
            float distanceScore = 1f - Mathf.Clamp01(distance / maxDistance);

            return angleScore * distanceScore;
        }

        #endregion
    }
}
