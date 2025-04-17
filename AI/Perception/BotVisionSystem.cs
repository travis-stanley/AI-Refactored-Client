#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Memory;
using System.Collections.Generic;
using AIRefactored.Core;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Handles bot vision detection logic, including ambient light checks, flash reactions,
    /// FOV-based scanning, and silhouette tracking. Mimics realistic player-like perception.
    /// </summary>
    public class BotVisionSystem : MonoBehaviour
    {
        #region Constants

        private const float VisionCheckInterval = 0.35f;
        private const float MaxSightDistance = 100f;
        private const float MinSightDistance = 15f;
        private const float DarknessThreshold = 0.12f;
        private const float FlashPanicThreshold = 0.9f;
        private const float MaxPlayerScanDistance = 120f;

        #endregion

        #region Private Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotVisionProfile? _profile;
        private float _nextVisionCheck = 0f;

        #endregion

        #region Unity Events

        /// <summary>
        /// Called once during initialization. Caches components and vision profile.
        /// </summary>
        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            if (_bot?.GetPlayer != null)
                _profile = BotVisionProfiles.Get(_bot.GetPlayer);
        }

        /// <summary>
        /// Called every frame to evaluate visibility and enemy detection.
        /// </summary>
        private void Update()
        {
            if (_bot == null || _cache == null || _profile == null || _bot.IsDead)
                return;

            // Stagger vision processing to improve performance
            if (Time.frameCount % 3 != _bot.ProfileId.GetHashCode() % 3)
                return;

            if (!GameWorldHandler.IsWithinPlayerRange(_bot.Position, MaxPlayerScanDistance))
                return;

            if (Time.time < _nextVisionCheck)
                return;

            _nextVisionCheck = Time.time + VisionCheckInterval;

            float ambient = GetSimulatedAmbientLight(out bool directGlare);

            float suppressionPenalty = _cache.Suppression?.IsSuppressed() == true ? 0.6f : 0f;
            float blindPenalty = _cache.IsBlinded ? 0.8f : 0f;
            if (directGlare) blindPenalty += 0.3f;

            if (blindPenalty >= FlashPanicThreshold)
            {
                _cache.PanicHandler?.TriggerPanic();
                return;
            }

            float cautionMod = 1f;
            float fovAngle = 110f;

            if (_cache.TryGetComponent(out AIRefactoredBotOwner? owner) && owner.PersonalityProfile != null)
            {
                float caution = owner.PersonalityProfile.Caution;
                cautionMod = Mathf.Lerp(0.7f, 1.3f, 1f - caution);

                if (caution > 0.7f || suppressionPenalty > 0.5f)
                    fovAngle = 85f;
            }

            float baseRange = Mathf.Lerp(MaxSightDistance, MinSightDistance, 1f - ambient * _profile.LightSensitivity);
            float adjustedRange = baseRange * cautionMod * (1f - Mathf.Clamp01(suppressionPenalty + blindPenalty));
            adjustedRange = Mathf.Clamp(adjustedRange, 10f, MaxSightDistance);

            _bot.LookSensor.ClearVisibleDist = adjustedRange;

            if (blindPenalty >= 0.75f || suppressionPenalty >= 0.6f)
                return;

            ScanForTargets(adjustedRange, ambient, fovAngle);
        }

        #endregion

        #region Vision Scanning

        /// <summary>
        /// Performs a cone check for visible players in front of the bot.
        /// </summary>
        private void ScanForTargets(float range, float ambient, float fov)
        {
            if (_bot?.EnemiesController == null || _bot.BotsGroup == null)
                return;

            Vector3 eye = _bot.Position + Vector3.up * 1.5f;
            Vector3 forward = _bot.LookDirection;
            float rangeSqr = range * range;

            foreach (Player player in GameWorldHandler.GetAllAlivePlayers())
            {
                if (player == null || player.ProfileId == _bot.ProfileId || _bot.EnemiesController.IsEnemy(player))
                    continue;

                if (!player.IsAI && player.IsYourPlayer)
                    continue;

                Vector3 toTarget = player.Position - eye;
                if (toTarget.sqrMagnitude > rangeSqr)
                    continue;

                float angle = Vector3.Angle(forward, toTarget);
                if (angle > fov * 0.5f)
                    continue;

                Vector3 dir = toTarget.normalized;
                if (!Physics.Raycast(eye, dir, out RaycastHit hit, range))
                    continue;

                if (hit.transform != player.Transform.Original)
                    continue;

                if (!CanSeeClearly(hit.point, ambient))
                {
                    TryTrackSilhouette(player, ambient);
                    continue;
                }

                if (_bot.BotsGroup.AddEnemy(player, EBotEnemyCause.addPlayer))
                    _bot.Memory?.AddEnemy(player, null, true);
            }
        }

        /// <summary>
        /// Adds enemy to memory if seen in silhouette during low light conditions.
        /// </summary>
        private void TryTrackSilhouette(Player player, float ambient)
        {
            if (_bot == null || _bot.Memory == null || ambient > 0.2f)
                return;

            float speed = player.MovementContext?.Velocity.magnitude ?? 0f;
            if (speed > 0.5f)
                _bot.Memory.AddEnemy(player, null, false);
        }

        #endregion

        #region Ambient Light Simulation

        /// <summary>
        /// Calculates an approximate light exposure including simulated glare from lights.
        /// </summary>
        private float GetSimulatedAmbientLight(out bool directGlare)
        {
            float baseAmbient = RenderSettings.ambientLight.grayscale;
            directGlare = false;

            Vector3 botEye = _bot!.Position + Vector3.up * 1.5f;

            foreach (Light light in GameObject.FindObjectsOfType<Light>())
            {
                if (!light.enabled || light.intensity < 1f || light.type != LightType.Spot)
                    continue;

                Vector3 toLight = light.transform.position - botEye;
                float dist = toLight.magnitude;
                if (dist > 15f) continue;

                if (Physics.Raycast(botEye, toLight.normalized, out RaycastHit hit, dist))
                {
                    if (hit.transform != light.transform && !hit.collider.isTrigger)
                        continue;
                }

                float angle = Vector3.Angle(-light.transform.forward, toLight.normalized);
                if (angle > 50f) continue;

                float intensity = Mathf.Clamp01(1f - (dist / 15f)) * light.intensity;
                baseAmbient += intensity * 0.5f;

                if (angle < 25f)
                    directGlare = true;
            }

            return Mathf.Clamp01(baseAmbient);
        }

        /// <summary>
        /// Determines if line of sight is clear of vision-blocking materials.
        /// </summary>
        private bool CanSeeClearly(Vector3 target, float ambient)
        {
            if (ambient < DarknessThreshold)
                return false;

            Vector3 origin = _bot!.Position + Vector3.up * 1.5f;
            Vector3 dir = (target - origin).normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, MaxSightDistance))
            {
                string tag = hit.collider.tag.ToLowerInvariant();
                string mat = hit.collider.sharedMaterial?.name.ToLowerInvariant() ?? "";

                if (tag.Contains("wall") || tag.Contains("glass") || mat.Contains("metal") || mat.Contains("concrete"))
                    return false;

                if (tag.Contains("bush") || mat.Contains("foliage") || mat.Contains("leaf"))
                    return false;

                return true;
            }

            return false;
        }

        #endregion
    }
}
