#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Tracks active tactical flashlights in the scene.
    /// Used by AI perception systems to simulate flashblindness, evasion, and visual discomfort.
    /// </summary>
    public static class FlashlightRegistry
    {
        #region Configuration

        private const float INTENSITY_THRESHOLD = 1.5f;
        private const float ANGLE_THRESHOLD = 60f;
        private const float FLASH_EXPOSE_CONE = 35f;
        private const float FLASH_EXPOSE_DIST = 28f;
        private const float FLASH_EXPOSE_RAY_BIAS = 0.22f;

        #endregion

        #region Internal State

        private static readonly List<Light> _activeLights = new List<Light>(32);
        private static readonly List<Vector3> _lastKnownFlashlightPositions = new List<Vector3>(32);
        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Scans for all tactical flashlights currently active in the scene and caches their positions.
        /// </summary>
        public static IEnumerable<Light> GetActiveFlashlights()
        {
            _activeLights.Clear();
            _lastKnownFlashlightPositions.Clear();

            Light[] lights = Object.FindObjectsOfType<Light>();
            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (IsTacticalFlashlight(light))
                {
                    _activeLights.Add(light);
                    _lastKnownFlashlightPositions.Add(light.transform.position);
                }
            }

            return _activeLights;
        }

        /// <summary>
        /// Returns cached positions of previously scanned tactical flashlights.
        /// </summary>
        public static IReadOnlyList<Vector3> GetLastKnownFlashlightPositions()
        {
            return _lastKnownFlashlightPositions;
        }

        /// <summary>
        /// Returns true if any tactical flashlight is currently exposing the bot's eyes.
        /// </summary>
        /// <param name="botHead">Head or eye-level transform of the bot.</param>
        /// <param name="blindingLight">The detected light causing blindness.</param>
        /// <param name="customMaxDist">Optional override for max detection distance.</param>
        public static bool IsExposingBot(Transform botHead, out Light? blindingLight, float customMaxDist = FLASH_EXPOSE_DIST)
        {
            blindingLight = null;
            if (botHead == null)
                return false;

            var lights = GetActiveFlashlights();
            Vector3 eyePos = botHead.position + Vector3.up * FLASH_EXPOSE_RAY_BIAS;

            foreach (var light in lights)
            {
                if (light == null || !light.enabled || !light.gameObject.activeInHierarchy)
                    continue;

                Vector3 lightToBot = eyePos - light.transform.position;
                float dist = lightToBot.magnitude;
                if (dist > customMaxDist)
                    continue;

                float coneAngle = Vector3.Angle(light.transform.forward, lightToBot);
                if (coneAngle > FLASH_EXPOSE_CONE)
                    continue;

                if (Physics.Raycast(light.transform.position, lightToBot.normalized, out RaycastHit hit, dist + 0.1f, LayerMaskClass.HighPolyWithTerrainMaskAI))
                {
                    if (hit.transform == botHead || hit.collider.transform == botHead)
                    {
                        blindingLight = light;
                        return true;
                    }
                }
                else if (coneAngle < (FLASH_EXPOSE_CONE / 2f) && dist < 4.5f)
                {
                    blindingLight = light;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns true if any flashlight is rapidly toggling or flickering.
        /// </summary>
        public static bool IsFlickeringFlashlightActive()
        {
            // [TODO]: Add flicker state tracking for future versions.
            return false;
        }

        #endregion

        #region Private Helpers

        /// <summary>
        /// Determines if a light qualifies as a tactical flashlight.
        /// </summary>
        private static bool IsTacticalFlashlight(Light light)
        {
            return light != null &&
                   light.enabled &&
                   light.type == LightType.Spot &&
                   light.intensity >= INTENSITY_THRESHOLD &&
                   light.spotAngle <= ANGLE_THRESHOLD &&
                   light.gameObject.activeInHierarchy;
        }

        #endregion
    }
}
