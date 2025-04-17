#nullable enable

using System.Collections.Generic;
using UnityEngine;
using EFT;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Scores nearby cover points based on visibility, material, angle to enemy, and exposure risk.
    /// Used by fallback and retreat systems to pick the best cover option.
    /// </summary>
    public static class CoverScorer
    {
        private const float MaxScore = 10f;
        private const float MinScore = 1f;

        private static readonly RaycastHit[] _hits = new RaycastHit[8];

        /// <summary>
        /// Evaluates the score of a potential fallback point based on its angle to the threat and surrounding geometry.
        /// </summary>
        /// <param name="bot">The bot evaluating fallback.</param>
        /// <param name="candidate">The candidate cover point.</param>
        /// <param name="threatDirection">Normalized direction of threat source.</param>
        /// <returns>Numerical score between 1 and 10. Higher is better.</returns>
        public static float ScoreCoverPoint(BotOwner bot, Vector3 candidate, Vector3 threatDirection)
        {
            float score = 1.0f;
            Vector3 origin = candidate + Vector3.up * 1.5f;

            // Step 1: Wall or object directly behind candidate?
            Vector3 opposite = -threatDirection.normalized;
            if (Physics.Raycast(origin, opposite, out var backHit, 3f))
            {
                if (IsSolid(backHit.collider))
                    score += 3f;
            }

            // Step 2: Is candidate facing an open hallway? Penalize.
            if (!Physics.Raycast(origin, threatDirection, 5f))
                score -= 2f;

            // Step 3: Check nearby left/right geometry for corner bonus
            Vector3 left = Quaternion.Euler(0f, -45f, 0f) * threatDirection;
            Vector3 right = Quaternion.Euler(0f, 45f, 0f) * threatDirection;

            if (Physics.Raycast(origin, left, out var lHit, 2f) && IsSolid(lHit.collider)) score += 1f;
            if (Physics.Raycast(origin, right, out var rHit, 2f) && IsSolid(rHit.collider)) score += 1f;

            // Step 4: Distance efficiency — penalize long runs
            float dist = Vector3.Distance(bot.Position, candidate);
            if (dist > 12f) score -= 1.5f;
            if (dist > 18f) score -= 2f;

            return Mathf.Clamp(score, MinScore, MaxScore);
        }

        /// <summary>
        /// Returns true if the collider represents solid, non-foliage cover.
        /// </summary>
        private static bool IsSolid(Collider collider)
        {
            if (collider == null) return false;

            string tag = collider.tag.ToLowerInvariant();
            string mat = collider.sharedMaterial?.name.ToLowerInvariant() ?? "";

            if (tag.Contains("glass") || tag.Contains("foliage") || mat.Contains("leaf") || mat.Contains("bush"))
                return false;

            return true;
        }
    }
}
