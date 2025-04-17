#nullable enable

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

        /// <summary>
        /// Evaluates the score of a potential fallback point based on geometry and threat direction.
        /// </summary>
        public static float ScoreCoverPoint(BotOwner bot, Vector3 candidate, Vector3 threatDirection)
        {
            float score = 1.0f;
            Vector3 origin = candidate + Vector3.up * 1.5f;

            Vector3 opposite = -threatDirection.normalized;

            // 1. Check for back wall protection
            if (Physics.Raycast(origin, opposite, out var backHit, 3f))
            {
                if (IsSolid(backHit.collider))
                    score += 3f;
            }

            // 2. Check if exposed to threat direction
            if (!Physics.Raycast(origin, threatDirection, 5f))
                score -= 2f;

            // 3. Bonus for flanking wall geometry
            Vector3 left = Quaternion.Euler(0f, -45f, 0f) * threatDirection;
            Vector3 right = Quaternion.Euler(0f, 45f, 0f) * threatDirection;

            if (Physics.Raycast(origin, left, out var lHit, 2f) && IsSolid(lHit.collider))
                score += 1f;

            if (Physics.Raycast(origin, right, out var rHit, 2f) && IsSolid(rHit.collider))
                score += 1f;

            // 4. Distance penalty (too far = risky)
            float dist = Vector3.Distance(bot.Position, candidate);
            if (dist > 12f) score -= 1.5f;
            if (dist > 18f) score -= 2.0f;

            return Mathf.Clamp(score, MinScore, MaxScore);
        }

        /// <summary>
        /// Returns true if the collider is solid, non-transparent, and non-foliage.
        /// </summary>
        private static bool IsSolid(Collider collider)
        {
            if (collider == null)
                return false;

            string tag = collider.tag.ToLowerInvariant();
            string mat = collider.sharedMaterial?.name.ToLowerInvariant() ?? "";

            return !(tag.Contains("glass") || tag.Contains("foliage") || mat.Contains("leaf") || mat.Contains("bush"));
        }
    }
}
