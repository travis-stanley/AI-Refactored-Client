#nullable enable

using AIRefactored.AI.Helpers;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Tracks tactical state deltas (aggression, caution, sneaky) and triggers behavior shifts.
    /// Used to detect and react to mid-mission personality changes.
    /// </summary>
    public class BotOwnerStateCache
    {
        #region Snapshot Struct

        /// <summary>
        /// Stores snapshot of bot’s personality values.
        /// </summary>
        private struct BotStateSnapshot
        {
            public float Aggression;
            public float Caution;
            public float Composure;
            public bool IsSneaky;

            public BotStateSnapshot(float aggression, float caution, float composure, bool isSneaky)
            {
                Aggression = aggression;
                Caution = caution;
                Composure = composure;
                IsSneaky = isSneaky;
            }

            public override bool Equals(object? obj)
            {
                if (obj is BotStateSnapshot other)
                {
                    return Mathf.Abs(Aggression - other.Aggression) < 0.05f &&
                           Mathf.Abs(Caution - other.Caution) < 0.05f &&
                           Mathf.Abs(Composure - other.Composure) < 0.05f &&
                           IsSneaky == other.IsSneaky;
                }

                return false;
            }

            public override int GetHashCode()
            {
                return (Aggression, Caution, Composure, IsSneaky).GetHashCode();
            }
        }

        #endregion

        #region Fields

        private readonly Dictionary<string, BotStateSnapshot> _cache = new Dictionary<string, BotStateSnapshot>();
        private static readonly bool EnableDebug = false;
        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Caches the bot’s tactical state on first evaluation.
        /// </summary>
        /// <param name="botOwner">Bot to evaluate and store state for.</param>
        public void CacheBotOwnerState(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            string? id = botOwner.Profile?.Id;
            if (string.IsNullOrEmpty(id))
                return;

            string key = id!;

            if (!_cache.ContainsKey(key))
            {
                _cache[key] = CaptureSnapshot(botOwner);
            }
        }


        /// <summary>
        /// Checks if the bot's state has changed and applies any necessary re-alignment.
        /// </summary>
        /// <param name="botOwner">Bot to monitor for tactical changes.</param>
        public void UpdateBotOwnerStateIfNeeded(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            string? id = botOwner.Profile?.Id;
            if (string.IsNullOrEmpty(id))
                return;

            string key = id!;

            var current = CaptureSnapshot(botOwner);

            if (_cache.TryGetValue(key, out var previous) && !previous.Equals(current))
            {
                _cache[key] = current;
                UpdateBotOwnerAI(botOwner, current);

                if (EnableDebug)
                {
                    string name = botOwner.Profile?.Info?.Nickname ?? key;
                    Logger.LogDebug($"[AIRefactored-State] {name} → Tactical shift: Agg={current.Aggression:F2} Cau={current.Caution:F2} Comp={current.Composure:F2}");
                }
            }
        }


        #endregion

        #region Snapshot Logic

        /// <summary>
        /// Captures current bot aggression, caution, composure, and sneaky values.
        /// </summary>
        /// <param name="botOwner">Bot from which to capture tactical state.</param>
        /// <returns>A snapshot struct of key AI values.</returns>
        private BotStateSnapshot CaptureSnapshot(BotOwner botOwner)
        {
            var profile = BotRegistry.Get(botOwner.ProfileId);
            var cache = BotCacheUtility.GetCache(botOwner);

            if (profile == null)
                return new BotStateSnapshot(0.5f, 0.5f, 1.0f, false);

            float composure = cache?.PanicHandler?.GetComposureLevel() ?? 1.0f;

            return new BotStateSnapshot(
                aggression: profile.AggressionLevel,
                caution: profile.Caution,
                composure: composure,
                isSneaky: profile.IsSilentHunter
            );
        }

        #endregion

        #region Behavior Adjustment

        /// <summary>
        /// Applies re-alignment logic based on tactical state shift.
        /// </summary>
        /// <param name="botOwner">Bot to modify behavior for.</param>
        /// <param name="snapshot">Current tactical state snapshot.</param>
        private void UpdateBotOwnerAI(BotOwner botOwner, BotStateSnapshot snapshot)
        {
            if (botOwner == null || botOwner.gameObject == null)
                return;

            // Shift toward forward zone if aggressive and composed
            if (snapshot.Aggression > 0.7f && snapshot.Composure > 0.8f)
            {
                ReassignZoneBehavior(botOwner, preferForward: true);
            }
            // Fall back slightly if very cautious or panicking
            else if (snapshot.Caution > 0.7f || snapshot.Composure < 0.3f)
            {
                ReassignZoneBehavior(botOwner, preferForward: false);
            }
            else
            {
                ReassignZoneBehavior(botOwner, preferForward: null);
            }
        }

        /// <summary>
        /// Adjusts movement to simulate zone reassignment based on bot's tactical orientation.
        /// </summary>
        /// <param name="botOwner">Bot to move.</param>
        /// <param name="preferForward">Whether to simulate aggressive advance or fallback behavior.</param>
        private void ReassignZoneBehavior(BotOwner botOwner, bool? preferForward)
        {
            Vector3 fallback = botOwner.Position + Vector3.back * 6f;
            Vector3 advance = botOwner.Position + Vector3.forward * 8f;

            if (preferForward == true)
            {
                BotMovementHelper.SmoothMoveTo(botOwner, advance, false, 1f);
            }
            else if (preferForward == false)
            {
                BotMovementHelper.SmoothMoveTo(botOwner, fallback, false, 1f);
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Checks whether bot is a real AI (not player or coop controlled).
        /// </summary>
        /// <param name="bot">Bot to verify.</param>
        /// <returns>True if the bot is an AI and not player-controlled.</returns>
        private static bool IsAIBot(BotOwner? bot)
        {
            var player = bot?.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        #endregion
    }
}
