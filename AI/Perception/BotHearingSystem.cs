#nullable enable

namespace AIRefactored.AI.Perception
{
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Memory;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Detects footsteps and gunfire from nearby real players, filtered by hearing loss and range.
    ///     Events are registered with tactical memory for situational awareness.
    /// </summary>
    public sealed class BotHearingSystem
    {
        private const float BaseHearingRange = 35f;

        private const float TimeWindow = 3f;

        private BotOwner? _bot;

        private BotComponentCache? _cache;

        /// <summary>
        ///     Initializes the hearing system with a bot's runtime cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache;
            this._bot = cache.Bot;
        }

        /// <summary>
        ///     Performs a hearing scan based on nearby real players and sound events.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!this.CanEvaluate())
                return;

            var volumeMod = this._cache?.HearingDamage?.VolumeModifier ?? 1f;
            var effectiveRange = BaseHearingRange * volumeMod;
            var effectiveRangeSqr = effectiveRange * effectiveRange;

            if (this._bot == null)
                return;

            var origin = this._bot.Position;
            var players = BotMemoryStore.GetNearbyPlayers(origin, effectiveRange);

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!IsValidAudibleTarget(player, origin, effectiveRangeSqr))
                    continue;

                if (this.HeardSomething(player, volumeMod)) this._cache?.RegisterHeardSound(player.Position);
            }
        }

        private static bool IsRealPlayer(Player player)
        {
            return player.AIData == null || !player.AIData.IsAI;
        }

        private static bool IsValidAudibleTarget(Player? target, Vector3 origin, float rangeSqr)
        {
            if (target == null || target.HealthController?.IsAlive != true)
                return false;

            if (!IsRealPlayer(target))
                return false;

            return (target.Position - origin).sqrMagnitude <= rangeSqr;
        }

        private bool CanEvaluate()
        {
            return this._bot is { IsDead: false, GetPlayer: not null } && this._bot.GetPlayer.IsAI;
        }

        private bool HeardSomething(Player player, float volumeMod)
        {
            if (this._bot == null)
                return false;

            return BotSoundUtils.DidFireRecently(this._bot, player, volumeMod, TimeWindow)
                   || BotSoundUtils.DidStepRecently(this._bot, player, volumeMod, TimeWindow);
        }
    }
}