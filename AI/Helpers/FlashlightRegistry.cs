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

        /// <summary>
        /// Scans all active Light components in the scene and caches valid spotlights.
        /// </summary>
        public static IEnumerable<Light> GetAllLights()
        {
            _activeLights.Clear();

            foreach (var light in GameObject.FindObjectsOfType<Light>())
            {
                if (light.enabled && light.type == LightType.Spot && light.intensity >= 1.5f)
                {
                    _activeLights.Add(light);
                }
            }

            return _activeLights;
        }

        /// <summary>
        /// Alias for GetAllLights — compatible with older scripts or external components.
        /// </summary>
        public static IEnumerable<Light> GetActiveFlashlights() => GetAllLights();
    }
}
