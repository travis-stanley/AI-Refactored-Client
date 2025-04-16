#nullable enable

using System.Collections.Generic;
using EFT;
using UnityEngine;
using AIRefactored.AI;
using AIRefactored.Core;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Applies group-based coordination logic and AI tuning to bots sharing a squad.
    /// Enhances cohesion, threat awareness, and group-level aggression scaling.
    /// </summary>
    public class BotOwnerGroupOptimization
    {
        public void OptimizeGroupAI(List<BotOwner> botOwners)
        {
            for (int i = 0; i < botOwners.Count; i++)
            {
                var botOwner = botOwners[i];
                if (botOwner?.Profile == null || botOwner.Settings?.FileSettings?.Mind == null)
                    continue;

                var profile = BotRegistry.Get(botOwner.Profile.Id);
                if (profile == null)
                    continue;

                var mind = botOwner.Settings.FileSettings.Mind;

                // Group cohesion affects vision & panic radius
                mind.DIST_TO_FOUND_SQRT = Mathf.Lerp(300f, 600f, 1f - profile.Cohesion);

                // Group proximity increases aggression scaling
                mind.FRIEND_AGR_KILL = Mathf.Clamp(mind.FRIEND_AGR_KILL + profile.AggressionLevel * 0.15f, 0f, 1f);

                // Improve threat detection as group gets tighter
                mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(mind.ENEMY_LOOK_AT_ME_ANG - profile.Cohesion * 5f, 5f, 30f);
            }
        }
    }
}
