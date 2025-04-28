#nullable enable

namespace AIRefactored.AI.Optimization
{
    using System.Collections.Generic;

    using AIRefactored.AI.Helpers;

    using EFT;

    using UnityEngine;

    /// <summary>
    /// Tracks tactical state deltas (aggression, caution, sneaky) and triggers behavior shifts.
    /// Used to detect and respond to mid-mission personality changes.
    /// </summary>
    public class BotOwnerStateCache
    {
        private readonly Dictionary<string, BotStateSnapshot> _cache = new(64);

        public void CacheBotOwnerState(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner) || botOwner.Profile == null)
                return;

            string id = botOwner.Profile.Id;
            if (string.IsNullOrEmpty(id) || this._cache.ContainsKey(id))
                return;

            this._cache[id] = this.CaptureSnapshot(botOwner);
        }

        public void UpdateBotOwnerStateIfNeeded(BotOwner botOwner)
        {
            if (!IsAIBot(botOwner) || botOwner.Profile == null)
                return;

            string id = botOwner.Profile.Id;
            if (string.IsNullOrEmpty(id))
                return;

            BotStateSnapshot current = this.CaptureSnapshot(botOwner);

            if (this._cache.TryGetValue(id, out BotStateSnapshot previous) && !previous.Equals(current))
            {
                this._cache[id] = current;
                this.ApplyStateChange(botOwner, current);
            }
        }

        private static bool IsAIBot(BotOwner? bot)
        {
            var player = bot?.GetPlayer;
            return player is { IsAI: true, IsYourPlayer: false };
        }

        private void ApplyStateChange(BotOwner botOwner, BotStateSnapshot snapshot)
        {
            bool isAggressive = snapshot.Aggression > 0.7f && snapshot.Composure > 0.8f;
            bool isCautious = snapshot.Caution > 0.7f || snapshot.Composure < 0.3f;

            if (isAggressive) this.TriggerZoneShift(botOwner, advance: true);
            else if (isCautious) this.TriggerZoneShift(botOwner, advance: false);
            else this.TriggerZoneShift(botOwner, advance: null);
        }

        private BotStateSnapshot CaptureSnapshot(BotOwner botOwner)
        {
            var profile = BotRegistry.Get(botOwner.ProfileId);
            var cache = BotCacheUtility.GetCache(botOwner);
            float composure = cache?.PanicHandler?.GetComposureLevel() ?? 1f;

            return profile == null
                       ? new BotStateSnapshot(0.5f, 0.5f, composure, false)
                       : new BotStateSnapshot(
                           profile.AggressionLevel,
                           profile.Caution,
                           composure,
                           profile.IsSilentHunter);
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

        private struct BotStateSnapshot
        {
            public float Aggression;

            public float Caution;

            public float Composure;

            public bool IsSneaky;

            public BotStateSnapshot(float aggression, float caution, float composure, bool isSneaky)
            {
                this.Aggression = aggression;
                this.Caution = caution;
                this.Composure = composure;
                this.IsSneaky = isSneaky;
            }

            public override bool Equals(object? obj)
            {
                return obj is BotStateSnapshot other && Mathf.Abs(this.Aggression - other.Aggression) < 0.05f
                                                     && Mathf.Abs(this.Caution - other.Caution) < 0.05f
                                                     && Mathf.Abs(this.Composure - other.Composure) < 0.05f
                                                     && this.IsSneaky == other.IsSneaky;
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 23 + this.Aggression.GetHashCode();
                    hash = hash * 23 + this.Caution.GetHashCode();
                    hash = hash * 23 + this.Composure.GetHashCode();
                    hash = hash * 23 + this.IsSneaky.GetHashCode();
                    return hash;
                }
            }
        }
    }
}