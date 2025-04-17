#nullable enable

using System.Collections.Generic;
using EFT;
using UnityEngine;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Tracks tactical state deltas (aggression/caution/stance intent) and triggers behavior shifts.
    /// Used for reacting to dynamic personality changes mid-mission.
    /// </summary>
    public class BotOwnerStateCache
    {
        #region Internal Snapshot Struct

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
                    return Mathf.Abs(Aggression - other.Aggression) < 0.05f
                        && Mathf.Abs(Caution - other.Caution) < 0.05f
                        && IsSneaky == other.IsSneaky;
                }

                return false;
            }

            public override int GetHashCode() => (Aggression, Caution, IsSneaky).GetHashCode();
        }

        #endregion

        #region Fields

        private readonly Dictionary<string, BotStateSnapshot> _cache = new();

        #endregion

        #region Public API

        /// <summary>
        /// Caches the bot’s tactical state on first encounter.
        /// </summary>
        public void CacheBotOwnerState(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            string id = botOwner.Profile?.Id ?? string.Empty;
            if (!_cache.ContainsKey(id))
            {
                _cache[id] = CaptureSnapshot(botOwner);
            }
        }

        /// <summary>
        /// Checks for significant changes in aggression/caution/stealth and triggers a zone behavior shift.
        /// </summary>
        public void UpdateBotOwnerStateIfNeeded(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner))
                return;

            string id = botOwner.Profile?.Id ?? string.Empty;
            var current = CaptureSnapshot(botOwner);

            if (_cache.TryGetValue(id, out var previous) && !previous.Equals(current))
            {
                _cache[id] = current;
                UpdateBotOwnerAI(botOwner, current);
            }
        }

        #endregion

        #region Logic

        private BotStateSnapshot CaptureSnapshot(BotOwner botOwner)
        {
            var profile = BotRegistry.Get(botOwner.ProfileId);
            if (profile == null)
                return new BotStateSnapshot(0.5f, 0.5f, false);

            return new BotStateSnapshot(
                profile.AggressionLevel,
                1f - profile.AggressionLevel,
                profile.IsSilentHunter
            );
        }

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

        private static bool IsAIBot(BotOwner bot)
        {
            var player = bot?.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        #endregion
    }
}
