#nullable enable

using EFT;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Applies squad-aware offset to movement destinations to prevent pathing collisions and clumping.
    /// Staggers formation based on bot index within the group.
    /// </summary>
    public class SquadPathCoordinator : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotsGroup? _group;

        private const float OffsetRadius = 2.5f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _group = _bot?.BotsGroup;
        }

        /// <summary>
        /// Returns a destination adjusted by this bot’s squad offset.
        /// </summary>
        public Vector3 ApplyOffsetTo(Vector3 sharedDestination)
        {
            return sharedDestination + GetCurrentOffset();
        }

        /// <summary>
        /// Gets this bot’s current formation offset.
        /// </summary>
        public Vector3 GetCurrentOffset()
        {
            if (_bot == null || _group == null || _group.MembersCount <= 1)
                return Vector3.zero;

            int myIndex = GetBotIndexInGroup();
            if (myIndex == -1)
                return Vector3.zero;

            float angleDeg = 60f * myIndex;
            float rad = angleDeg * Mathf.Deg2Rad;

            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * OffsetRadius;
        }

        /// <summary>
        /// Gets this bot’s index within its BotsGroup. Returns -1 if not found.
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
