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

        #endregion

        #region Scoring Logic

        /// <summary>
        /// Evaluates a world position as a potential fallback point based on cover and threat exposure.
        /// </summary>
        /// <param name="bot">The evaluating bot.</param>
        /// <param name="candidate">World position to test.</param>
        /// <param name="threatDirection">Direction of threat (enemy position - bot position).</param>
        /// <returns>Float score representing cover quality (1–10).</returns>
        public static float ScoreCoverPoint(BotOwner bot, Vector3 candidate, Vector3 threatDirection)
        {
            float score = 1.0f;
            Vector3 eyeLevel = candidate + Vector3.up * 1.5f;
            Vector3 reverseThreat = -threatDirection.normalized;

            // 1. Bonus if there's solid back-wall protection
            if (Physics.Raycast(eyeLevel, reverseThreat, out RaycastHit backHit, BackWallCheckDistance))
            {
                if (IsSolid(backHit.collider))
                    score += 3f;
            }

            // 2. Penalty if point is exposed toward threat
            if (!Physics.Raycast(eyeLevel, threatDirection.normalized, ExposureDistance))
                score -= 2f;

            // 3. Bonus if flank sides have geometry protection
            Vector3[] flankDirections =
            {
                Quaternion.Euler(0f, -60f, 0f) * threatDirection,
                Quaternion.Euler(0f, -30f, 0f) * threatDirection,
                Quaternion.Euler(0f, 30f, 0f) * threatDirection,
                Quaternion.Euler(0f, 60f, 0f) * threatDirection
            };

            foreach (var flank in flankDirections)
            {
                if (Physics.Raycast(eyeLevel, flank.normalized, out RaycastHit flankHit, FlankRayDistance))
                {
                    if (IsSolid(flankHit.collider))
                        score += 0.5f;
                }
            }

            // 4. Distance penalty to reduce risky or excessive fallback
            float dist = Vector3.Distance(bot.Position, candidate);
            if (dist > IdealFallbackDistance)
            {
                float penalty = Mathf.Clamp((dist - IdealFallbackDistance) * 0.2f, 0f, 3.0f);
                score -= penalty;
            }

            return Mathf.Clamp(score, MinScore, MaxScore);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Returns true if the surface is solid and not transparent (e.g., not glass, foliage, soft banners).
        /// </summary>
        private static bool IsSolid(Collider collider)
        {
            if (collider == null)
                return false;

            string tag = collider.tag.ToLowerInvariant();
            string material = collider.sharedMaterial != null ? collider.sharedMaterial.name.ToLowerInvariant() : "";

            return !(tag.Contains("glass") || tag.Contains("foliage") || tag.Contains("banner") ||
                     material.Contains("leaf") || material.Contains("bush") || material.Contains("net") || material.Contains("fabric"));
        }

        #endregion
    }
}
