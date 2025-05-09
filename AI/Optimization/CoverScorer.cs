﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

#nullable enable

namespace AIRefactored.AI.Optimization
{
    using AIRefactored.Runtime;
    using BepInEx.Logging;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Scores fallback points based on terrain, threat exposure, wall coverage, and distance.
    /// Used by AI retreat and cover systems to evaluate safe fallback zones under fire.
    /// </summary>
    public static class CoverScorer
    {
        #region Constants

        private const float BackWallDistance = 3.0f;
        private const float ExposureCheckDistance = 5.0f;
        private const float EyeHeightOffset = 1.5f;
        private const float FlankRayDistance = 2.5f;
        private const float IdealFallbackDistance = 8.0f;
        private const float MaxScore = 10.0f;
        private const float MinScore = 1.0f;

        #endregion

        #region Static Fields

        private static readonly Vector3[] FlankAngles =
        {
            new Vector3(-60f, 0f, 0f),
            new Vector3(-30f, 0f, 0f),
            new Vector3(30f, 0f, 0f),
            new Vector3(60f, 0f, 0f)
        };

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Evaluates a fallback point for tactical use.
        /// </summary>
        /// <param name="bot">Bot evaluating cover.</param>
        /// <param name="candidate">Candidate position.</param>
        /// <param name="threatDirection">Direction of incoming fire or threat.</param>
        /// <returns>Score between 1 and 10 based on tactical safety.</returns>
        public static float ScoreCoverPoint(BotOwner bot, Vector3 candidate, Vector3 threatDirection)
        {
            if (bot == null)
            {
                return MinScore;
            }

            Vector3 eyePos = candidate + (Vector3.up * EyeHeightOffset);
            Vector3 toThreat = threatDirection.normalized;
            Vector3 fromThreat = -toThreat;

            float score = 1.0f;

            // Wall behind bonus
            if (Physics.Raycast(eyePos, fromThreat, out RaycastHit backHit, BackWallDistance) &&
                IsSolid(backHit.collider))
            {
                score += 3.0f;
            }

            // Exposure penalty
            if (!Physics.Raycast(eyePos, toThreat, ExposureCheckDistance))
            {
                score -= 2.0f;
            }

            // Flank protection bonus
            for (int i = 0; i < FlankAngles.Length; i++)
            {
                Vector3 flankDir = Quaternion.Euler(0f, FlankAngles[i].x, 0f) * toThreat;

                if (Physics.Raycast(eyePos, flankDir.normalized, out RaycastHit flankHit, FlankRayDistance) &&
                    IsSolid(flankHit.collider))
                {
                    score += 0.5f;
                }
            }

            // Distance modifier
            float dist = Vector3.Distance(bot.Position, candidate);
            if (dist > IdealFallbackDistance)
            {
                float excess = dist - IdealFallbackDistance;
                score -= Mathf.Min(excess * 0.25f, 3.0f);
            }

            Logger.LogDebug($"[CoverScorer] Score={score:F2} @ {candidate} | From={bot.Position} | Dir={toThreat}");

            return Mathf.Clamp(score, MinScore, MaxScore);
        }

        #endregion

        #region Internal API

        /// <summary>
        /// Determines whether a collider represents solid, safe cover.
        /// Rejects glass, foliage, fabric, small triggers, etc.
        /// </summary>
        /// <param name="collider">Collider to test.</param>
        /// <returns>True if considered solid tactical cover.</returns>
        internal static bool IsSolid(Collider? collider)
        {
            if (collider == null || collider.isTrigger)
            {
                return false;
            }

            if (collider.bounds.size.magnitude < 0.2f)
            {
                return false;
            }

            string tag = collider.tag?.ToLowerInvariant() ?? string.Empty;
            string mat = collider.sharedMaterial?.name?.ToLowerInvariant() ?? string.Empty;

            if (tag.Contains("glass") || tag.Contains("foliage") || tag.Contains("banner") || tag.Contains("transparent"))
            {
                return false;
            }

            if (mat.Contains("leaf") || mat.Contains("bush") || mat.Contains("net") ||
                mat.Contains("fabric") || mat.Contains("cloth") || mat.Contains("tarp"))
            {
                return false;
            }

            return true;
        }

        #endregion
    }
}
