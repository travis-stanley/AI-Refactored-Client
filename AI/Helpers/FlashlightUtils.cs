#nullable enable

namespace AIRefactored.AI.Helpers
{
    using UnityEngine;

    /// <summary>
    ///     Evaluates directional exposure to high-intensity light sources like flashlights and flares.
    ///     Used by AI vision systems for flashblindness detection, light evasion, and behavioral reactions.
    /// </summary>
    public static class FlashLightUtils
    {
        /// <summary>
        ///     Returns a visibility score between 0 and 1 based on angle and distance to flashlight.
        ///     Combines frontal alignment and proximity to determine severity of exposure.
        /// </summary>
        public static float CalculateFlashScore(
            Transform? lightTransform,
            Transform? botHeadTransform,
            float maxDistance = 20f)
        {
            if (lightTransform == null || botHeadTransform == null)
                return 0f;

            var toLight = lightTransform.position - botHeadTransform.position;
            var distance = toLight.magnitude;

            if (distance < 0.01f || distance > maxDistance)
                return 0f;

            var angleFactor = Mathf.Clamp01(Vector3.Dot(botHeadTransform.forward.normalized, toLight.normalized));
            var distanceFactor = 1f - Mathf.Clamp01(distance / maxDistance);

            return angleFactor * distanceFactor;
        }

        /// <summary>
        ///     Calculates the normalized frontal exposure to a flashlight.
        /// </summary>
        /// <param name="lightTransform">Transform of the light source.</param>
        /// <param name="botHeadTransform">Bot head or camera transform.</param>
        /// <returns>1.0 if fully facing the light, 0.0 if no direct alignment.</returns>
        public static float GetFlashIntensityFactor(Transform? lightTransform, Transform? botHeadTransform)
        {
            if (lightTransform == null || botHeadTransform == null)
                return 0f;

            var toLight = (lightTransform.position - botHeadTransform.position).normalized;
            var forward = botHeadTransform.forward.normalized;

            return Mathf.Clamp01(Vector3.Dot(forward, toLight));
        }

        /// <summary>
        ///     Determines whether the bot is facing a light source within a dangerous exposure cone.
        /// </summary>
        /// <param name="lightTransform">Transform of the flashlight source.</param>
        /// <param name="botHeadTransform">Transform of the bot's head or camera.</param>
        /// <param name="angleThreshold">Max angle (degrees) for exposure to count.</param>
        /// <returns>True if the bot is being blinded by the light source.</returns>
        public static bool IsBlindingLight(
            Transform? lightTransform,
            Transform? botHeadTransform,
            float angleThreshold = 30f)
        {
            if (lightTransform == null || botHeadTransform == null)
                return false;

            var toLight = lightTransform.position - botHeadTransform.position;
            var angle = Vector3.Angle(botHeadTransform.forward, toLight);
            return angle <= angleThreshold;
        }

        /// <summary>
        ///     Returns true if the light source is pointing toward the bot.
        /// </summary>
        public static bool IsFacingTarget(Transform? source, Transform? target, float angleThreshold = 30f)
        {
            if (source == null || target == null)
                return false;

            var toTarget = target.position - source.position;
            var angle = Vector3.Angle(source.forward, toTarget);
            return angle <= angleThreshold;
        }
    }
}