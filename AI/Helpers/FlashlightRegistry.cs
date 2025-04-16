#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Tracks all active flashlight sources in the scene for AI perception.
    /// Used by bots to react to visual overexposure and simulate flash blindness.
    /// </summary>
    public static class FlashlightRegistry
    {
        private static readonly List<Light> _activeLights = new();

        private const float INTENSITY_THRESHOLD = 1.5f;
        private const float ANGLE_THRESHOLD = 60f; // Spotlights narrower than this are likely tactical

        /// <summary>
        /// Scans all active Light components in the scene and caches valid spotlights.
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

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored-FlashlightRegistry] Found {_activeLights.Count} valid tactical flashlights.");
#endif

            return _activeLights;
        }

        /// <summary>
        /// Determines whether a light qualifies as a tactical flashlight.
        /// </summary>
        private static bool IsTacticalFlashlight(Light light)
        {
            return light.enabled &&
                   light.type == LightType.Spot &&
                   light.intensity >= INTENSITY_THRESHOLD &&
                   light.spotAngle <= ANGLE_THRESHOLD;
        }

        /// <summary>
        /// Alias for GetAllLights — compatible with older scripts or external components.
        /// </summary>
        public static IEnumerable<Light> GetActiveFlashlights() => GetAllLights();
    }
}
