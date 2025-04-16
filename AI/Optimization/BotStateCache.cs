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
        private readonly Dictionary<string, BotOwnerState> _cache = new();

        /// <summary>
        /// Adds a bot's current state into cache (if not already present).
        /// </summary>
        public void CacheBotOwnerState(BotOwner botOwner)
        {
            string id = botOwner?.Profile?.Id ?? "";
            if (!_cache.ContainsKey(id))
            {
                _cache[id] = new BotOwnerState(botOwner);
            }
        }

        /// <summary>
        /// Compares current bot state to cached. If changed, updates and triggers AI update logic.
        /// </summary>
        public void UpdateBotOwnerStateIfNeeded(BotOwner botOwner)
        {
            string id = botOwner?.Profile?.Id ?? "";
            var newState = new BotOwnerState(botOwner);

            if (_cache.TryGetValue(id, out var lastState) && !lastState.Equals(newState))
            {
                _cache[id] = newState;
                UpdateBotOwnerAI(botOwner);
            }
        }

        private void UpdateBotOwnerAI(BotOwner botOwner)
        {
            if (botOwner == null || botOwner.gameObject == null)
                return;

            var profile = BotRegistry.Get(botOwner.ProfileId);
            if (profile == null)
                return;

            float aggression = profile.AggressionLevel;
            float caution = 1f - aggression;

            // Personality-driven position adjustment
            if (aggression > 0.7f)
            {
#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-StateCache] {botOwner.Profile?.Info?.Nickname} acting aggressively.");
#endif
                ReassignZoneBehavior(botOwner, preferForward: true);
            }
            else if (caution > 0.7f)
            {
#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-StateCache] {botOwner.Profile?.Info?.Nickname} acting cautiously.");
#endif
                ReassignZoneBehavior(botOwner, preferForward: false);
            }
            else
            {
#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-StateCache] {botOwner.Profile?.Info?.Nickname} acting neutrally.");
#endif
                ReassignZoneBehavior(botOwner, preferForward: null);
            }
        }

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
            else
            {
#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-StateCache] {botOwner.Profile?.Info?.Nickname} is reevaluating (neutral zone).");
#endif
            }
        }
    }
}
