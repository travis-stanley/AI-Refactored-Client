#nullable enable

namespace AIRefactored.AI.Optimization
{
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Scores fallback points based on terrain, threat exposure, wall coverage, and distance.
    ///     Used by AI retreat and cover systems to evaluate safe fallback zones under fire.
    /// </summary>
    public static class CoverScorer
    {
        private const float BackWallDistance = 3f;

        private const float ExposureCheckDistance = 5f;

        private const float EyeHeightOffset = 1.5f;

        private const float FlankRayDistance = 2.5f;

        private const float IdealFallbackDistance = 8f;

        private const float MaxScore = 10f;

        private const float MinScore = 1f;

        // Flank ray angles to simulate wide-angle threat coverage
        private static readonly Vector3[] FlankAngles =
            {
                new(-60f, 0f, 0f), new(-30f, 0f, 0f), new(30f, 0f, 0f), new(60f, 0f, 0f)
            };

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        /// <summary>
        ///     Evaluates a fallback point for tactical use.
        /// </summary>
        /// <param name="bot">Bot evaluating cover.</param>
        /// <param name="candidate">Candidate position.</param>
        /// <param name="threatDirection">Direction of incoming fire or threat.</param>
        public static float ScoreCoverPoint(BotOwner bot, Vector3 candidate, Vector3 threatDirection)
        {
            var eyePos = candidate + Vector3.up * EyeHeightOffset;
            var toThreat = threatDirection.normalized;
            var fromThreat = -toThreat;

            var score = 1f;

            // Wall behind = bonus for wall protection
            if (Physics.Raycast(eyePos, fromThreat, out var backHit, BackWallDistance) && IsSolid(backHit.collider))
                score += 3f;

            // Front exposure = penalty if no obstacle between eye and threat
            if (!Physics.Raycast(eyePos, toThreat, ExposureCheckDistance))
                score -= 2f;

            // Flank protection = bonus for lateral obstacles
            foreach (var angle in FlankAngles)
            {
                var flankDir = Quaternion.Euler(0f, angle.x, 0f) * toThreat;
                if (Physics.Raycast(eyePos, flankDir, out var flankHit, FlankRayDistance) && IsSolid(flankHit.collider))
                    score += 0.5f;
            }

            // Distance efficiency (favor tactical fallback distance)
            var distance = Vector3.Distance(bot.Position, candidate);
            if (distance > IdealFallbackDistance)
            {
                var excess = distance - IdealFallbackDistance;
                var penalty = Mathf.Min(excess * 0.25f, 3f);
                score -= penalty;
            }

            Logger.LogDebug(
                $"[CoverScorer] Score={score:F2} at {candidate} | From={bot.Position} | ThreatDir={threatDirection.normalized}");

            return Mathf.Clamp(score, MinScore, MaxScore);
        }

        /// <summary>
        ///     Determines whether a collider represents solid cover.
        ///     Ignores transparent, foliage, or weak materials.
        /// </summary>
        internal static bool IsSolid(Collider? collider)
        {
            if (collider == null)
                return false;

            // Require the material name and tag to both avoid known soft types
            var tag = collider.tag.ToLowerInvariant();
            var mat = collider.sharedMaterial?.name.ToLowerInvariant() ?? string.Empty;

            // Reject soft or decorative materials
            if (tag.Contains("glass") || tag.Contains("foliage") || tag.Contains("banner")
                || tag.Contains("transparent"))
                return false;

            if (mat.Contains("leaf") || mat.Contains("bush") || mat.Contains("net") || mat.Contains("fabric")
                || mat.Contains("cloth"))
                return false;

            // Optional: reject triggers or special logic layers (if collider is non-physical)
            if (collider.isTrigger)
                return false;

            // Optional: reject very thin walls (< threshold)
            if (collider.bounds.size.magnitude < 0.2f)
                return false;

            return true;
        }
    }
}