#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Tracks active tactical flashlight sources for AI visual processing.
    /// Used by bots to simulate flashblindness and evasive behavior.
    /// </summary>
    public static class FlashlightRegistry
    {
        #region Configuration

        /// <summary>
        /// Minimum light intensity required to be considered a flashlight.
        /// </summary>
        private const float INTENSITY_THRESHOLD = 1.5f;

        /// <summary>
        /// Maximum spotlight cone angle to be considered tactical (narrow beam).
        /// </summary>
        private const float ANGLE_THRESHOLD = 60f;

        #endregion

        #region Internal Cache

        /// <summary>
        /// Internal list of valid lights cached per query to reduce allocation.
        /// </summary>
        private static readonly List<Light> _activeLights = new List<Light>(32);

        #endregion

        #region Public Access

        /// <summary>
        /// Returns all valid tactical flashlight sources in the scene.
        /// </summary>
        public static IEnumerable<Light> GetActiveFlashlights()
        {
            _activeLights.Clear();
            var lights = Object.FindObjectsOfType<Light>();

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (IsTacticalFlashlight(light))
                {
                    _activeLights.Add(light);
                }
            }

            return _activeLights;
        }

        #endregion

        #region Classification Logic

        /// <summary>
        /// Returns true if the light qualifies as a tactical flashlight.
        /// </summary>
        private static bool IsTacticalFlashlight(Light light)
        {
            return light != null &&
                   light.enabled &&
                   light.type == LightType.Spot &&
                   light.intensity >= INTENSITY_THRESHOLD &&
                   light.spotAngle <= ANGLE_THRESHOLD;
        }

        #endregion
    }
}
