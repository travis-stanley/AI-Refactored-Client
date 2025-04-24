#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Detects footsteps and gunfire from nearby real players, filtered by hearing loss and range.
    /// Events are registered with tactical memory for situational awareness.
    /// </summary>
    public sealed class BotHearingSystem
    {
        #region Constants

        private const float BaseHearingRange = 35f;
        private const float TimeWindow = 3f;

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the hearing system with a bot's runtime cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Tick

        /// <summary>
        /// Performs a hearing scan based on nearby real players and sound events.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!CanEvaluate())
                return;

            float volumeMod = _cache?.HearingDamage?.VolumeModifier ?? 1f;
            float effectiveRange = BaseHearingRange * volumeMod;
            float effectiveRangeSqr = effectiveRange * effectiveRange;

            if (_bot == null)
                return;

            Vector3 origin = _bot.Position;
            List<Player> players = BotMemoryStore.GetNearbyPlayers(origin, effectiveRange);

            for (int i = 0; i < players.Count; i++)
            {
                Player player = players[i];
                if (!IsValidAudibleTarget(player, origin, effectiveRangeSqr))
                    continue;

                if (HeardSomething(player, volumeMod))
                {
                    _cache?.RegisterHeardSound(player.Position);
                }
            }
        }

        #endregion

        #region Evaluation

        private bool CanEvaluate()
        {
            return _bot is { IsDead: false, GetPlayer: not null } &&
                   _bot.GetPlayer.IsAI;
        }

        private static bool IsValidAudibleTarget(Player? target, Vector3 origin, float rangeSqr)
        {
            if (target == null || target.HealthController?.IsAlive != true)
                return false;

            if (!IsRealPlayer(target))
                return false;

            return (target.Position - origin).sqrMagnitude <= rangeSqr;
        }

        private bool HeardSomething(Player player, float volumeMod)
        {
            if (_bot == null)
                return false;

            return BotSoundUtils.DidFireRecently(_bot, player, volumeMod, TimeWindow) ||
                   BotSoundUtils.DidStepRecently(_bot, player, volumeMod, TimeWindow);
        }

        private static bool IsRealPlayer(Player player)
        {
            return player.AIData == null || !player.AIData.IsAI;
        }

        #endregion
    }
}
