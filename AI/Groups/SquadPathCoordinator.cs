#nullable enable

namespace AIRefactored.AI.Groups
{
    using AIRefactored.AI.Core;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Offsets squad member movement targets to prevent clumping.
    ///     Applies radial spacing patterns based on squad index and stabilized spread.
    /// </summary>
    public sealed class SquadPathCoordinator
    {
        private const float BaseSpacing = 2.25f;

        private const float MaxSpacing = 6.5f;

        private const float MinSpacing = 1.25f;

        private BotOwner? _bot;

        private Vector3 _cachedOffset = Vector3.zero;

        private BotsGroup? _group;

        private int _lastGroupSize = -1;

        private bool _offsetInitialized;

        /// <summary>
        ///     Returns a destination offset from a shared group destination, unique to this bot.
        /// </summary>
        public Vector3 ApplyOffsetTo(Vector3 sharedDestination)
        {
            return sharedDestination + this.GetCurrentOffset();
        }

        /// <summary>
        ///     Gets this bot's current spacing offset from group destination.
        ///     Recomputes if group size changes or not initialized.
        /// </summary>
        public Vector3 GetCurrentOffset()
        {
            if (this._bot == null || this._group == null)
                return Vector3.zero;

            var groupSize = this._group.MembersCount;

            if (!this._offsetInitialized || groupSize != this._lastGroupSize)
            {
                this._cachedOffset = this.ComputeOffset();
                this._offsetInitialized = true;
                this._lastGroupSize = groupSize;
            }

            return this._cachedOffset;
        }

        /// <summary>
        ///     Initializes the squad path logic for the given bot cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            if (cache == null)
                return;

            this._bot = cache.Bot;
            this._group = this._bot?.BotsGroup;
            this._offsetInitialized = false;
        }

        private static int GetBotIndexInGroup(BotOwner bot, BotsGroup group)
        {
            for (var i = 0; i < group.MembersCount; i++)
            {
                var member = group.Member(i);
                if (member != null && member.ProfileId == bot.ProfileId)
                    return i;
            }

            return -1;
        }

        private Vector3 ComputeOffset()
        {
            if (this._bot == null || this._group == null || this._group.MembersCount < 2)
                return Vector3.zero;

            var index = GetBotIndexInGroup(this._bot, this._group);
            if (index < 0)
                return Vector3.zero;

            var total = this._group.MembersCount;

            // Deterministic seed per-bot for squad-consistent offset
            var seed = this._bot.ProfileId.GetHashCode() ^ total;
            Random.InitState(seed);

            var spacing = Mathf.Clamp(BaseSpacing + Random.Range(-0.4f, 0.4f), MinSpacing, MaxSpacing);
            var angleStep = 360f / total;
            var angle = index * angleStep + Random.Range(-8f, 8f);
            var radians = angle * Mathf.Deg2Rad;

            return new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * spacing;
        }
    }
}