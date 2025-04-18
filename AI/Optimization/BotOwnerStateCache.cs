#nullable enable

using AIRefactored.AI.Helpers;
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
            public bool IsSneaky;

            public BotStateSnapshot(float aggression, float caution, bool isSneaky)
            {
                Aggression = aggression;
                Caution = caution;
                IsSneaky = isSneaky;
            }

            public override bool Equals(object? obj)
            {
                if (obj is BotStateSnapshot other)
                {
                    return Mathf.Abs(Aggression - other.Aggression) < 0.05f &&
                           Mathf.Abs(Caution - other.Caution) < 0.05f &&
                           IsSneaky == other.IsSneaky;
                }

                return false;
            }

            public override int GetHashCode()
            {
                return (Aggression, Caution, IsSneaky).GetHashCode();
            }
        }

        #endregion

        #region Fields

        private readonly Dictionary<string, BotStateSnapshot> _cache = new Dictionary<string, BotStateSnapshot>();

        #endregion

        #region Public API

        /// <summary>
        /// Caches the bot’s tactical state on first evaluation.
        /// </summary>
        /// <param name="botOwner">The bot to cache.</param>
        public void CacheBotOwnerState(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner) || string.IsNullOrEmpty(botOwner.Profile?.Id))
                return;

            string id = botOwner.Profile.Id;

            if (!_cache.ContainsKey(id))
            {
                _cache[id] = CaptureSnapshot(botOwner);
            }
        }

        /// <summary>
        /// Checks if the bot's state has changed and applies any necessary re-alignment.
        /// </summary>
        /// <param name="botOwner">The bot to evaluate and potentially update.</param>
        public void UpdateBotOwnerStateIfNeeded(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner) || string.IsNullOrEmpty(botOwner.Profile?.Id))
                return;

            string id = botOwner.Profile.Id;
            var current = CaptureSnapshot(botOwner);

            if (_cache.TryGetValue(id, out var previous) && !previous.Equals(current))
            {
                _cache[id] = current;
                UpdateBotOwnerAI(botOwner, current);
            }
        }

        #endregion

        #region Snapshot Logic

        /// <summary>
        /// Captures current bot aggression, caution, and sneaky values.
        /// </summary>
        private BotStateSnapshot CaptureSnapshot(BotOwner botOwner)
        {
            var profile = BotRegistry.Get(botOwner.ProfileId);

            if (profile == null)
                return new BotStateSnapshot(0.5f, 0.5f, false);

            return new BotStateSnapshot(
                aggression: profile.AggressionLevel,
                caution: 1f - profile.AggressionLevel,
                isSneaky: profile.IsSilentHunter
            );
        }

        #endregion

        #region Behavior Adjustment

        /// <summary>
        /// Applies re-alignment logic based on tactical state shift.
        /// </summary>
        private void UpdateBotOwnerAI(BotOwner botOwner, BotStateSnapshot snapshot)
        {
            if (botOwner == null || botOwner.gameObject == null)
                return;

            if (snapshot.Aggression > 0.7f)
            {
                ReassignZoneBehavior(botOwner, preferForward: true);
            }
            else if (snapshot.Caution > 0.7f)
            {
                ReassignZoneBehavior(botOwner, preferForward: false);
            }
            else
            {
                ReassignZoneBehavior(botOwner, preferForward: null);
            }
        }

        /// <summary>
        /// Adjusts movement to simulate zone reassignment based on shift in bot's attitude.
        /// </summary>
        private void ReassignZoneBehavior(BotOwner botOwner, bool? preferForward)
        {
            Vector3 fallback = botOwner.Position + Vector3.back * 5f;
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
        private static bool IsAIBot(BotOwner bot)
        {
            var player = bot?.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        #endregion
    }
}
