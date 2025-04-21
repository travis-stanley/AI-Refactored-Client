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
        #region Fields

        private BotOwner? _bot;
        private BotsGroup? _group;

        private const float BaseSpacing = 2.25f;
        private const float MinSpacing = 1.25f;
        private const float MaxSpacing = 6.5f;

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the squad path coordinator using the provided bot component cache.
        /// </summary>
        /// <param name="cache">Reference to the bot's AI component cache.</param>
        public void Initialize(BotComponentCache cache)
        {
            _bot = cache.Bot;
            _group = _bot?.BotsGroup;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Applies a squad-based offset to a target destination to prevent unit overlap.
        /// </summary>
        /// <param name="sharedDestination">The squad’s common destination.</param>
        /// <returns>Offset destination for this specific bot.</returns>
        public Vector3 ApplyOffsetTo(Vector3 sharedDestination)
        {
            return sharedDestination + GetCurrentOffset();
        }

        /// <summary>
        /// Calculates the current squad offset for this bot based on its index and staggered radius.
        /// </summary>
        /// <returns>Offset vector to apply to target destination.</returns>
        public Vector3 GetCurrentOffset()
        {
            if (_bot == null || _group == null || _group.MembersCount <= 1)
                return Vector3.zero;

            int index = GetBotIndexInGroup();
            if (index == -1)
            {
                _log.LogWarning($"[SquadPathCoordinator] Could not determine index for bot {_bot.Profile?.Info?.Nickname ?? "unknown"}.");
                return RandomInsideCircle(1.25f); // Fallback jitter
            }

            float spacing = Mathf.Clamp(BaseSpacing + Random.Range(-0.4f, 0.4f), MinSpacing, MaxSpacing);
            float angleStep = 360f / Mathf.Max(2, _group.MembersCount);
            float angleDeg = index * angleStep + Random.Range(-12f, 12f);
            float radians = angleDeg * Mathf.Deg2Rad;

            return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * spacing;
        }

        #endregion

        #region Internal Helpers

        /// <summary>
        /// Attempts to retrieve this bot’s current index within the BotsGroup.
        /// </summary>
        /// <returns>Index in the group, or -1 if not found.</returns>
        private int GetBotIndexInGroup()
        {
            if (_bot == null || _group == null)
                return -1;

            for (int i = 0; i < _group.MembersCount; i++)
            {
                var member = _group.Member(i);
                if (member != null && member.ProfileId == _bot.ProfileId)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Generates a random offset in a circular radius for fallback spacing.
        /// </summary>
        /// <param name="radius">Circle radius for random point generation.</param>
        /// <returns>Vector offset in circle plane.</returns>
        private Vector3 RandomInsideCircle(float radius)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
        }

        #endregion
    }
}
