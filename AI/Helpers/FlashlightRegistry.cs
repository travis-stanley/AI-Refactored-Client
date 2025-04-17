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
        #region Cached Lights

        private static readonly List<Light> _activeLights = new();

        /// <summary>
        /// Minimum light intensity required to be considered a flashlight.
        /// </summary>
        private const float INTENSITY_THRESHOLD = 1.5f;

        /// <summary>
        /// Maximum spotlight cone angle to be considered tactical (narrow beam).
        /// </summary>
        private const float ANGLE_THRESHOLD = 60f;

        #endregion

        #region Public Queries

        /// <summary>
        /// Returns all valid flashlight sources in the current scene.
        /// Caches results each frame to avoid repeated allocation.
        /// </summary>
        public static IEnumerable<Light> GetAllLights()
        {
            _activeLights.Clear();

            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                if (IsTacticalFlashlight(light))
                {
                    _activeLights.Add(light);
                }
            }

            return _activeLights;
        }

        /// <summary>
        /// Alias for GetAllLights, maintained for compatibility.
        /// </summary>
        public static IEnumerable<Light> GetActiveFlashlights() => GetAllLights();

        #endregion

        #region Classification Logic

        /// <summary>
        /// Determines whether a given light matches tactical flashlight criteria.
        /// </summary>
        private static bool IsTacticalFlashlight(Light light)
        {
            return light.enabled &&
                   light.type == LightType.Spot &&
                   light.intensity >= INTENSITY_THRESHOLD &&
                   light.spotAngle <= ANGLE_THRESHOLD;
        }

        #endregion
    }
}
