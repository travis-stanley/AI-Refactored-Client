#nullable enable

using AIRefactored.AI.Core;
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
        /// </summary>
        public Vector3 GetCurrentOffset()
        {
            if (_bot == null || _group == null || _group.MembersCount <= 1)
                return Vector3.zero;

            int myIndex = GetBotIndexInGroup();
            if (myIndex == -1)
                return Vector3.zero;

            float spacing = Mathf.Clamp(OffsetRadius, MinSpacing, 6f);
            float angleStep = 360f / Mathf.Max(2, _group.MembersCount);
            float angleDeg = myIndex * angleStep;
            float rad = angleDeg * Mathf.Deg2Rad;

            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * spacing;
        }

        /// <summary>
        /// Gets the index of this bot within its group. Returns -1 if not found.
        /// </summary>
        private int GetBotIndexInGroup()
        {
            if (_bot == null || _group == null)
                return -1;

            for (int i = 0; i < _group.MembersCount; i++)
            {
                if (_group.Member(i) == _bot)
                    return i;
            }

            return -1;
        }
    }
}
