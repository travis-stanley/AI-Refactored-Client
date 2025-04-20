#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Applies squad-aware offset to movement destinations to prevent pathing collisions and clumping.
    /// Dynamically staggers formation using radial patterns based on bot index.
    /// </summary>
    public class SquadPathCoordinator
    {
        private BotOwner? _bot;
        private BotsGroup? _group;

        private const float OffsetRadius = 2.25f;
        private const float MinSpacing = 1.25f;
        private const float MaxSpacing = 6.5f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        /// <summary>
        /// Initializes the squad coordinator using the bot's component cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _bot = cache.Bot;
            _group = _bot?.BotsGroup;
        }

        /// <summary>
        /// Returns a destination adjusted by this bot’s squad offset to reduce clumping.
        /// </summary>
        public Vector3 ApplyOffsetTo(Vector3 sharedDestination)
        {
            return sharedDestination + GetCurrentOffset();
        }

        /// <summary>
        /// Calculates a radial offset based on this bot’s position in the squad.
        /// Adds slight variation for de-syncing perfect formations.
        /// </summary>
        public Vector3 GetCurrentOffset()
        {
            if (_bot == null || _group == null || _group.MembersCount <= 1)
                return Vector3.zero;

            int myIndex = GetBotIndexInGroup();
            if (myIndex == -1)
            {
                Logger.LogWarning($"[SquadPathCoordinator] Could not find bot index for {_bot.ProfileId}, applying random fallback offset.");
                return UnityEngine.Random.insideUnitSphere * 1.25f;  // Fallback if bot index is not found
            }

            float spacing = Mathf.Clamp(OffsetRadius + UnityEngine.Random.Range(-0.3f, 0.3f), MinSpacing, MaxSpacing);
            float angleStep = 360f / Mathf.Max(2, _group.MembersCount);
            float angleDeg = myIndex * angleStep + UnityEngine.Random.Range(-10f, 10f);
            float rad = angleDeg * Mathf.Deg2Rad;

            // Return the calculated offset as a position in a radial pattern
            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * spacing;
        }

        /// <summary>
        /// Gets the index of this bot within its group. Returns -1 if not found.
        /// </summary>
        private int GetBotIndexInGroup()
        {
            if (_bot == null || _group == null)
                return -1;

            // Iterate through group members and return the bot's index if found
            for (int i = 0; i < _group.MembersCount; i++)
            {
                var member = _group.Member(i);
                if (member != null && member.ProfileId == _bot.ProfileId)
                    return i;
            }

            return -1;  // Return -1 if the bot is not found in the group
        }
    }
}
