#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Tracks and exposes active tactical flashlights in the scene.
    /// Used by AI perception systems for flashblindness, evasion, and visual panic triggers.
    /// </summary>
    public static class FlashlightRegistry
    {
        #region Constants

        private const float IntensityThreshold = 1.5f;
        private const float AngleThreshold = 60f;
        private const float ExposureConeAngle = 35f;
        private const float MaxExposureDistance = 28f;
        private const float EyeRayBias = 0.22f;

        #endregion

        #region State

        private static readonly List<Light> _activeLights = new List<Light>(32);
        private static readonly List<Vector3> _lastKnownFlashPositions = new List<Vector3>(32);

        #endregion

        #region Public API

        /// <summary>
        /// Scans the scene for active tactical flashlights.
        /// </summary>
        public static IEnumerable<Light> GetActiveFlashlights()
        {
            _activeLights.Clear();
            _lastKnownFlashPositions.Clear();

            var allLights = Object.FindObjectsOfType<Light>();
            for (int i = 0; i < allLights.Length; i++)
            {
                Light light = allLights[i];
                if (IsValidTacticalLight(light))
                {
                    _activeLights.Add(light);
                    _lastKnownFlashPositions.Add(light.transform.position);
                }
            }

            return _activeLights;
        }

        /// <summary>
        /// Returns the last-known positions of visible flashlights.
        /// </summary>
        public static IReadOnlyList<Vector3> GetLastKnownFlashlightPositions() => _lastKnownFlashPositions;

        /// <summary>
        /// Returns true if any flashlight is currently hitting this bot in the eyes.
        /// </summary>
        public static bool IsExposingBot(Transform botHead, out Light? blindingLight, float customMaxDist = MaxExposureDistance)
        {
            blindingLight = null;
            if (botHead == null)
                return false;

            Vector3 eyePos = botHead.position + Vector3.up * EyeRayBias;

            foreach (Light light in _activeLights)
            {
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                    continue;

                Vector3 toBot = eyePos - light.transform.position;
                float distance = toBot.magnitude;
                if (distance > customMaxDist)
                    continue;

                float angle = Vector3.Angle(light.transform.forward, toBot);
                if (angle > ExposureConeAngle)
                    continue;

                if (Physics.Raycast(
                    light.transform.position,
                    toBot.normalized,
                    out RaycastHit hit,
                    distance + 0.1f,
                    LayerMaskClass.HighPolyWithTerrainMaskAI))
                {
                    if (hit.transform == botHead || hit.collider.transform == botHead)
                    {
                        blindingLight = light;
                        return true;
                    }
                }
                else if (angle < (ExposureConeAngle / 2f) && distance < 4.5f)
                {
                    blindingLight = light;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Whether any flashlight is flickering (not implemented yet).
        /// </summary>
        public static bool IsFlickeringFlashlightActive()
        {
            // Reserved for future realism upgrades (e.g. flicker-based fear triggers)
            return false;
        }

        #endregion

        #region Helpers

        private static bool IsValidTacticalLight(Light light)
        {
            return light != null &&
                   light.enabled &&
                   light.type == LightType.Spot &&
                   light.intensity >= IntensityThreshold &&
                   light.spotAngle <= AngleThreshold &&
                   light.gameObject.activeInHierarchy;
        }

        #endregion
    }
}
