#nullable enable

using AIRefactored.AI.Helpers;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Tracks tactical state deltas (aggression, caution, sneaky) and triggers behavior shifts.
    /// Used to detect and respond to mid-mission personality changes.
    /// </summary>
    public class BotOwnerStateCache
    {
        #region Internal Types

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
                return obj is BotStateSnapshot other &&
                       Mathf.Abs(Aggression - other.Aggression) < 0.05f &&
                       Mathf.Abs(Caution - other.Caution) < 0.05f &&
                       Mathf.Abs(Composure - other.Composure) < 0.05f &&
                       IsSneaky == other.IsSneaky;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + Aggression.GetHashCode();
                    hash = hash * 23 + Caution.GetHashCode();
                    hash = hash * 23 + Composure.GetHashCode();
                    hash = hash * 23 + IsSneaky.GetHashCode();
                    return hash;
                }
            }
        }

        #endregion

        #region Fields

        private readonly Dictionary<string, BotStateSnapshot> _cache = new(64);

        #endregion

        #region Public API

        public void CacheBotOwnerState(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner) || botOwner.Profile == null)
                return;

            string id = botOwner.Profile.Id;
            if (string.IsNullOrEmpty(id) || _cache.ContainsKey(id))
                return;

            _cache[id] = CaptureSnapshot(botOwner);
        }

        public void UpdateBotOwnerStateIfNeeded(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner) || botOwner.Profile == null)
                return;

            string id = botOwner.Profile.Id;
            if (string.IsNullOrEmpty(id))
                return;

            BotStateSnapshot current = CaptureSnapshot(botOwner);

            if (_cache.TryGetValue(id, out BotStateSnapshot previous) && !previous.Equals(current))
            {
                _cache[id] = current;
                ApplyStateChange(botOwner, current);
            }
        }

        #endregion

        #region Snapshot Logic

        private BotStateSnapshot CaptureSnapshot(BotOwner botOwner)
        {
            var profile = BotRegistry.Get(botOwner.ProfileId);
            var cache = BotCacheUtility.GetCache(botOwner);
            float composure = cache?.PanicHandler?.GetComposureLevel() ?? 1f;

            return profile == null
                ? new BotStateSnapshot(0.5f, 0.5f, composure, false)
                : new BotStateSnapshot(profile.AggressionLevel, profile.Caution, composure, profile.IsSilentHunter);
        }

        #endregion

        #region Behavior Realignment

        private void ApplyStateChange(BotOwner botOwner, BotStateSnapshot snapshot)
        {
            bool isAggressive = snapshot.Aggression > 0.7f && snapshot.Composure > 0.8f;
            bool isCautious = snapshot.Caution > 0.7f || snapshot.Composure < 0.3f;

            if (isAggressive)
                TriggerZoneShift(botOwner, advance: true);
            else if (isCautious)
                TriggerZoneShift(botOwner, advance: false);
            else
                TriggerZoneShift(botOwner, advance: null);
        }

        private void TriggerZoneShift(BotOwner botOwner, bool? advance)
        {
            if (botOwner.Transform == null)
                return;

            Vector3 shift = advance switch
            {
                true => botOwner.Transform.forward * 8f,
                false => -botOwner.Transform.forward * 6f,
                _ => Vector3.zero
            };

            if (shift.sqrMagnitude > 0.1f)
            {
                Vector3 target = botOwner.Position + shift;
                BotMovementHelper.SmoothMoveTo(botOwner, target);
            }
        }

        #endregion

        #region Utility

        private static bool IsAIBot(BotOwner? bot)
        {
            var player = bot?.GetPlayer;
            return player is { IsAI: true, IsYourPlayer: false };
        }

        #endregion
    }
}
