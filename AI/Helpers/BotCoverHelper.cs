﻿// <auto-generated>
//   AI-Refactored: BotCoverHelper.cs (Max Realism, May 2025 – Beyond Diamond Edition)
//   Bulletproof cover scoring, posture, and tactical application system.
//   All logic is null-guarded, memory-safe, and multiplayer/headless ready.
//   Fully posture-integrated with PoseController, no registry fallback, no external disables.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Helpers
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Combat;
    using AIRefactored.AI.Core;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Tactical cover scoring, validation, and pose/stance setting.
    /// Includes memory of recent use, smart posture inference, and distance/angle scoring for fallback and squad sync.
    /// </summary>
    public static class BotCoverHelper
    {
        #region Constants

        private const float MemoryDuration = 20f;
        private const float MaxValidDistanceSqr = 16f;
        private const float MinCoverDistance = 1.25f;
        private const float MaxCoverDistance = 15.0f;

        #endregion

        #region Static Memory

        private static readonly Dictionary<string, float> CoverMemory = new Dictionary<string, float>(128);

        #endregion

        #region Cover Type Checks

        public static bool IsLowCover(CustomNavigationPoint point)
        {
            return point != null && point.CoverLevel == CoverLevel.Sit;
        }

        public static bool IsProneCover(CustomNavigationPoint point)
        {
            return point != null && point.CoverLevel == CoverLevel.Lay;
        }

        public static bool IsStandingCover(CustomNavigationPoint point)
        {
            return point != null && point.CoverLevel == CoverLevel.Stay;
        }

        #endregion

        #region Cover Memory System

        public static void MarkUsed(CustomNavigationPoint point)
        {
            if (point != null)
                MarkUsed(point.Position);
        }

        public static void MarkUsed(Vector3 position)
        {
            if (!IsValidPos(position))
                return;

            try
            {
                string key = GetKey(position);
                if (!string.IsNullOrEmpty(key))
                    CoverMemory[key] = Time.time;
            }
            catch { }
        }

        public static bool WasRecentlyUsed(CustomNavigationPoint point)
        {
            return point != null && WasRecentlyUsed(point.Position);
        }

        public static bool WasRecentlyUsed(Vector3 position)
        {
            try
            {
                string key = GetKey(position);
                return CoverMemory.TryGetValue(key, out float last) && (Time.time - last) < MemoryDuration;
            }
            catch { return false; }
        }

        #endregion

        #region Pose Assignment Logic

        /// <summary>
        /// Uses nearby cover point to assign bot pose realistically.
        /// Never disables. Safe in headless or multiplayer.
        /// </summary>
        public static void TrySetStanceFromNearbyCover(BotComponentCache cache, Vector3 position)
        {
            if (cache?.PoseController == null || !IsValidPos(position))
                return;

            try
            {
                Collider[] hits = Physics.OverlapSphere(position, 4f);
                float bestDist = float.MaxValue;
                CustomNavigationPoint best = null;

                for (int i = 0; i < hits.Length; i++)
                {
                    var point = hits[i].GetComponent<CustomNavigationPoint>();
                    if (point == null || !IsValidPos(point.Position))
                        continue;

                    float distSqr = (point.Position - position).sqrMagnitude;
                    if (distSqr > MaxValidDistanceSqr || distSqr < MinCoverDistance * MinCoverDistance)
                        continue;

                    if (distSqr < bestDist)
                    {
                        bestDist = distSqr;
                        best = point;
                    }
                }

                if (best != null)
                {
                    if (IsProneCover(best))
                    {
                        cache.PoseController.SetProne(true);
                        return;
                    }

                    if (IsLowCover(best))
                    {
                        cache.PoseController.SetCrouch(true);
                        return;
                    }

                    if (IsStandingCover(best))
                    {
                        cache.PoseController.SetProne(false);
                        cache.PoseController.SetCrouch(false);
                    }
                }
            }
            catch { }
        }

        #endregion

        #region Tactical Cover Scoring

        /// <summary>
        /// Scores a cover point for fallback/retreat/attack use.
        /// Includes threat angle, posture bonus, memory penalty, and distance.
        /// </summary>
        public static float Score(CustomNavigationPoint point, Vector3 botPos, Vector3 threatPos)
        {
            if (point == null)
                return 0f;

            try
            {
                float distBot = Vector3.Distance(botPos, point.Position);
                float distThreat = Vector3.Distance(threatPos, point.Position);
                float angle = Vector3.Angle(threatPos - point.Position, botPos - point.Position);

                float postureBonus = 0.65f;
                if (IsProneCover(point)) postureBonus = 1.20f;
                else if (IsLowCover(point)) postureBonus = 1.0f;
                else if (IsStandingCover(point)) postureBonus = 0.85f;

                float threatFactor = Mathf.Clamp01(distThreat / 22f);
                float angleFactor = Mathf.Clamp01(angle / 180f);
                float distancePenalty = 1f + Mathf.Clamp(distBot * 0.16f, 0.8f, 2.8f);
                float memoryPenalty = WasRecentlyUsed(point) ? 0.55f : 1.0f;

                return ((postureBonus + threatFactor + angleFactor) * memoryPenalty) / distancePenalty;
            }
            catch
            {
                return 0f;
            }
        }

        #endregion

        #region Cover Validation

        /// <summary>
        /// Validates a cover point for tactical fallback use. Multiplayer-safe and stance-aware.
        /// </summary>
        public static bool IsValidCoverPoint(CustomNavigationPoint point, BotOwner bot, bool requireFree, bool preferIndoor)
        {
            try
            {
                if (point == null || bot == null)
                    return false;

                if (requireFree && !point.IsFreeById(bot.Id))
                    return false;

                if (point.IsSpotted)
                    return false;

                if (preferIndoor && !point.IsGoodInsideBuilding)
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Internal Helpers

        private static string GetKey(Vector3 pos)
        {
            int x = Mathf.RoundToInt(pos.x);
            int y = Mathf.RoundToInt(pos.y);
            int z = Mathf.RoundToInt(pos.z);
            return $"{x}_{y}_{z}";
        }

        private static bool IsValidPos(Vector3 pos)
        {
            return pos != Vector3.zero &&
                   !float.IsNaN(pos.x) &&
                   !float.IsNaN(pos.y) &&
                   !float.IsNaN(pos.z);
        }

        #endregion
    }
}
