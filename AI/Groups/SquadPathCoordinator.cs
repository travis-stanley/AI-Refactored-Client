#nullable enable

using AIRefactored.AI.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Offsets squad member movement targets to prevent clumping.
    /// Applies radial spacing patterns based on squad index and stabilized spread.
    /// </summary>
    public sealed class SquadPathCoordinator
    {
        #region Constants

        private const float BaseSpacing = 2.25f;
        private const float MinSpacing = 1.25f;
        private const float MaxSpacing = 6.5f;

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotsGroup? _group;
        private Vector3 _cachedOffset = Vector3.zero;
        private bool _offsetInitialized;
        private int _lastGroupSize = -1;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the squad path logic for the given bot cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            if (cache == null)
                return;

            _bot = cache.Bot;
            _group = _bot?.BotsGroup;
            _offsetInitialized = false;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Returns a destination offset from a shared group destination, unique to this bot.
        /// </summary>
        public Vector3 ApplyOffsetTo(Vector3 sharedDestination)
        {
            return sharedDestination + GetCurrentOffset();
        }

        /// <summary>
        /// Gets this bot's current spacing offset from group destination.
        /// Recomputes if group size changes or not initialized.
        /// </summary>
        public Vector3 GetCurrentOffset()
        {
            if (_bot == null || _group == null)
                return Vector3.zero;

            int groupSize = _group.MembersCount;

            if (!_offsetInitialized || groupSize != _lastGroupSize)
            {
                _cachedOffset = ComputeOffset();
                _offsetInitialized = true;
                _lastGroupSize = groupSize;
            }

            return _cachedOffset;
        }

        #endregion

        #region Offset Computation

        private Vector3 ComputeOffset()
        {
            if (_bot == null || _group == null || _group.MembersCount < 2)
                return Vector3.zero;

            int index = -1;
            int total = _group.MembersCount;

            for (int i = 0; i < total; i++)
            {
                var member = _group.Member(i);
                if (member != null && member.ProfileId == _bot.ProfileId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
                return Vector3.zero;

            // Realistic spread using seeded radial jitter
            int seed = _bot.ProfileId.GetHashCode() ^ total;
            UnityEngine.Random.InitState(seed);

            float spacing = Mathf.Clamp(BaseSpacing + UnityEngine.Random.Range(-0.4f, 0.4f), MinSpacing, MaxSpacing);
            float angleStep = 360f / total;
            float angle = index * angleStep + UnityEngine.Random.Range(-8f, 8f);
            float rad = angle * Mathf.Deg2Rad;

            return new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * spacing;
        }

        #endregion
    }
}
