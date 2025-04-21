#nullable enable

using EFT;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Scores fallback points based on terrain, exposure to threat, wall coverage, and approach distance.
    /// Used by AI retreat and cover systems to evaluate the best positions under fire.
    /// </summary>
    public static class CoverScorer
    {
        #region Config

        private const float MaxScore = 10f;
        private const float MinScore = 1f;

        private const float BackWallCheckDistance = 3f;
        private const float ExposureDistance = 5f;
        private const float FlankRayDistance = 2.5f;

        private const float MaxEffectiveRange = 25f;
        private const float IdealFallbackDistance = 8f;

        private static readonly bool EnableDebug = false;

        #endregion

        #region Scoring Logic

        /// <summary>
        /// Evaluates a world position as a potential fallback point based on cover and threat exposure.
        /// </summary>
        /// <param name="bot">Bot evaluating the fallback.</param>
        /// <param name="candidate">The fallback point to score.</param>
        /// <param name="threatDirection">Direction from which the threat is coming.</param>
        /// <returns>A score between 1 and 10 indicating tactical value of the cover point.</returns>
        public static float ScoreCoverPoint(BotOwner bot, Vector3 candidate, Vector3 threatDirection)
        {
            float score = 1.0f;
            Vector3 eyeLevel = candidate + Vector3.up * 1.5f;
            Vector3 reverseThreat = -threatDirection.normalized;

            // 1. Back wall cover bonus
            if (Physics.Raycast(eyeLevel, reverseThreat, out RaycastHit backHit, BackWallCheckDistance))
            {
                if (IsSolid(backHit.collider))
                    score += 3f;
            }

            // 2. Exposure penalty — no obstacle between candidate and threat direction
            if (!Physics.Raycast(eyeLevel, threatDirection.normalized, ExposureDistance))
            {
                score -= 2f;
            }

            // 3. Flank coverage bonus
            Vector3[] flankDirections =
            {
                Quaternion.Euler(0f, -60f, 0f) * threatDirection,
                Quaternion.Euler(0f, -30f, 0f) * threatDirection,
                Quaternion.Euler(0f, 30f, 0f) * threatDirection,
                Quaternion.Euler(0f, 60f, 0f) * threatDirection
            };

            for (int i = 0; i < flankDirections.Length; i++)
            {
                Vector3 flankDir = flankDirections[i].normalized;
                if (Physics.Raycast(eyeLevel, flankDir, out RaycastHit flankHit, FlankRayDistance))
                {
                    if (IsSolid(flankHit.collider))
                        score += 0.5f;
                }
            }

            // 4. Distance penalty — too far is risky
            float dist = Vector3.Distance(bot.Position, candidate);
            if (dist > IdealFallbackDistance)
            {
                float penalty = Mathf.Clamp((dist - IdealFallbackDistance) * 0.25f, 0f, 3f);
                score -= penalty;
            }

            if (EnableDebug)
            {
                Debug.DrawRay(eyeLevel, reverseThreat * BackWallCheckDistance, Color.red, 0.25f);
                Debug.DrawRay(eyeLevel, threatDirection.normalized * ExposureDistance, Color.yellow, 0.25f);
            }

            return Mathf.Clamp(score, MinScore, MaxScore);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Determines if the surface is opaque and solid enough for cover.
        /// Filters out transparent or soft materials.
        /// </summary>
        /// <param name="collider">The collider hit by raycast.</param>
        /// <returns>True if considered solid cover.</returns>
        private static bool IsSolid(Collider? collider)
        {
            if (collider == null)
                return false;

            string tag = collider.tag.ToLowerInvariant();
            string matName = collider.sharedMaterial != null ? collider.sharedMaterial.name.ToLowerInvariant() : "";

            if (tag.Contains("glass") || tag.Contains("foliage") || tag.Contains("banner"))
                return false;

            if (matName.Contains("leaf") || matName.Contains("bush") || matName.Contains("net") || matName.Contains("fabric"))
                return false;

            return true;
        }

        #endregion
    }
}
