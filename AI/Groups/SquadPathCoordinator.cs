#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Core;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Applies squad-aware offset to movement destinations to prevent pathing collisions and clumping.
    /// Staggers formation based on squad size and group position.
    /// </summary>
    public class SquadPathCoordinator : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private int _groupIndex = 0;
        private int _groupSize = 1;
        private Vector3 _lastOffset = Vector3.zero;

        private const float MaxOffset = 3.5f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();

            if (_bot?.BotsGroup != null)
            {
                _groupSize = _bot.BotsGroup.MembersCount;
                for (int i = 0; i < _groupSize; i++)
                {
                    var member = _bot.BotsGroup.Member(i);
                    if (member == _bot)
                    {
                        _groupIndex = i;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a destination offset based on this bot’s index in its squad.
        /// Call this when moving toward a common squad objective.
        /// </summary>
        public Vector3 GetOffset()
        {
            if (_groupSize <= 1)
                return Vector3.zero;

            float angle = 360f * (_groupIndex / (float)_groupSize);
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            _lastOffset = offset.normalized * MaxOffset * 0.5f;

            return _lastOffset;
        }

        /// <summary>
        /// Applies the current offset to a shared destination.
        /// </summary>
        public Vector3 ApplyOffsetTo(Vector3 sharedDestination)
        {
            return sharedDestination + GetOffset();
        }

        /// <summary>
        /// Returns the current raw offset vector (cached).
        /// </summary>
        public Vector3 GetCurrentOffset()
        {
            return _lastOffset;
        }
    }
}
