#nullable enable

using System.Collections.Generic;
using EFT;
using UnityEngine;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Tracks state deltas for bot owners (e.g., aggression/perception changes) and triggers behavior shifts.
    /// </summary>
    public class BotOwnerStateCache
    {
        #region Fields

        private readonly Dictionary<string, BotOwnerState> _cache = new();

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a bot's current state into cache (if not already present).
        /// </summary>
        public void CacheBotOwnerState(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            string id = botOwner?.Profile?.Id ?? string.Empty;
            if (!_cache.ContainsKey(id))
            {
                _cache[id] = new BotOwnerState(botOwner);
            }
        }

        /// <summary>
        /// Compares current bot state to cached. If changed, updates and triggers AI logic.
        /// </summary>
        public void UpdateBotOwnerStateIfNeeded(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            string id = botOwner?.Profile?.Id ?? string.Empty;
            var newState = new BotOwnerState(botOwner);

            if (_cache.TryGetValue(id, out var lastState) && !lastState.Equals(newState))
            {
                _cache[id] = newState;
                UpdateBotOwnerAI(botOwner);
            }
        }

        #endregion

        #region Internal Logic

        /// <summary>
        /// Triggers tactical behavior reassignment based on updated aggression/caution.
        /// </summary>
        private void UpdateBotOwnerAI(BotOwner botOwner)
        {
            if (botOwner == null || botOwner.gameObject == null)
                return;

            var profile = BotRegistry.Get(botOwner.ProfileId);
            if (profile == null)
                return;

            float aggression = profile.AggressionLevel;
            float caution = 1f - aggression;

            if (aggression > 0.7f)
            {
                ReassignZoneBehavior(botOwner, preferForward: true);
            }
            else if (caution > 0.7f)
            {
                ReassignZoneBehavior(botOwner, preferForward: false);
            }
            else
            {
                ReassignZoneBehavior(botOwner, preferForward: null);
            }
        }

        /// <summary>
        /// Issues movement to fallback or advance position based on personality profile.
        /// </summary>
        private void ReassignZoneBehavior(BotOwner botOwner, bool? preferForward)
        {
            Vector3 fallback = botOwner.Position + Vector3.back * 5f;
            Vector3 advance = botOwner.Position + Vector3.forward * 8f;

            if (preferForward == true)
            {
                botOwner.Mover?.GoToPoint(advance, false, 1f);
            }
            else if (preferForward == false)
            {
                botOwner.Mover?.GoToPoint(fallback, false, 1f);
            }
        }

        /// <summary>
        /// Ensures the logic does not affect player-controlled or coop players.
        /// </summary>
        private static bool IsAIBot(BotOwner bot)
        {
            var player = bot?.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        #endregion
    }
}
