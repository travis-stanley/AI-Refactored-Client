#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Listens for nearby sounds like gunfire or movement and registers them with bot memory.
    /// Adjusted by hearing damage and auditory range modifiers.
    /// </summary>
    public class BotHearingSystem
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private float _baseRange = 35f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;
        private static readonly bool DebugEnabled = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the hearing system with bot context and cache.
        /// </summary>
        /// <param name="cache">Bot component cache reference.</param>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Runtime Tick

        /// <summary>
        /// Called from BotBrain.Tick() to check for new sound sources.
        /// </summary>
        /// <param name="deltaTime">Time since last tick.</param>
        public void Tick(float deltaTime)
        {
            if (_bot == null || _bot.IsDead || _bot.GetPlayer?.IsYourPlayer == true)
                return;

            var player = _bot.GetPlayer;
            if (player == null || !player.IsAI)
                return;

            Vector3 myPos = _bot.Position;
            float deafnessScale = _cache?.HearingDamage?.VolumeModifier ?? 1f;
            float maxRange = _baseRange * deafnessScale;

            List<Player> players = BotMemoryStore.GetNearbyPlayers(myPos);

            for (int i = 0; i < players.Count; i++)
            {
                var other = players[i];
                if (other == null || other == player || other.HealthController?.IsAlive != false)
                    continue;

                // Only react to non-player, non-dead, valid sound emitters
                if (!other.IsAI && !other.IsYourPlayer)
                    continue;

                float dist = Vector3.Distance(myPos, other.Position);
                if (dist > maxRange)
                    continue;

                if (!BotSoundUtils.DidFireRecently(other) && !BotSoundUtils.DidStepRecently(other))
                    continue;

                _cache?.RegisterHeardSound(other.Position);

                if (DebugEnabled)
                {
                    string botName = _bot?.Profile?.Info?.Nickname ?? "Bot";
                    string target = other.Profile?.Info?.Nickname ?? "Unknown";
                    Logger.LogDebug($"[BotHearing] {botName} heard {target} at {dist:F1}m.");
                }
            }
        }

        #endregion
    }
}
