#nullable enable

namespace AIRefactored.AI.Helpers
{
    using System.Collections.Generic;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Central registry for gunfire and footstep timestamps and locations.
    ///     Bots use this for directional awareness, hearing checks, and zone-based escalation.
    /// </summary>
    public static class BotSoundRegistry
    {
        private const float DefaultHearingRadius = 30f;

        private static readonly Dictionary<string, float> _footstepTimestamps = new(64);

        private static readonly Dictionary<string, float> _shotTimestamps = new(64);

        private static readonly Dictionary<string, Vector3> _soundZones = new(64);

        public static void Clear()
        {
            _shotTimestamps.Clear();
            _footstepTimestamps.Clear();
            _soundZones.Clear();
        }

        public static bool FiredRecently(Player? player, float withinSeconds = 1.5f, float now = -1f)
        {
            return TryGetLastShot(player, out var time) && (now >= 0f ? now : Time.time) - time <= withinSeconds;
        }

        /// <summary>
        ///     Records a gunshot sound and position.
        /// </summary>
        public static void NotifyShot(Player? player)
        {
            if (!IsTrackable(player)) return;

            var id = player!.ProfileId;
            _shotTimestamps[id] = Time.time;
            _soundZones[id] = player.Position;

            TriggerSquadPing(id, player.Position, true);
        }

        /// <summary>
        ///     Records a footstep sound and position.
        /// </summary>
        public static void NotifyStep(Player? player)
        {
            if (!IsTrackable(player)) return;

            var id = player!.ProfileId;
            _footstepTimestamps[id] = Time.time;
            _soundZones[id] = player.Position;

            TriggerSquadPing(id, player.Position, false);
        }

        public static bool SteppedRecently(Player? player, float withinSeconds = 1.2f, float now = -1f)
        {
            return TryGetLastStep(player, out var time) && (now >= 0f ? now : Time.time) - time <= withinSeconds;
        }

        public static bool TryGetLastShot(Player? player, out float time)
        {
            time = -1f;
            return player != null && !string.IsNullOrEmpty(player.ProfileId)
                                  && _shotTimestamps.TryGetValue(player.ProfileId, out time);
        }

        public static bool TryGetLastStep(Player? player, out float time)
        {
            time = -1f;
            return player != null && !string.IsNullOrEmpty(player.ProfileId)
                                  && _footstepTimestamps.TryGetValue(player.ProfileId, out time);
        }

        public static bool TryGetSoundPosition(Player? player, out Vector3 pos)
        {
            pos = Vector3.zero;
            return player != null && !string.IsNullOrEmpty(player.ProfileId)
                                  && _soundZones.TryGetValue(player.ProfileId, out pos);
        }

        private static bool IsTrackable(Player? player)
        {
            return player != null && !player.IsYourPlayer && !string.IsNullOrEmpty(player.ProfileId);
        }

        /// <summary>
        ///     Broadcasts to nearby bots so they can log or react to the sound source.
        /// </summary>
        private static void TriggerSquadPing(string sourceId, Vector3 location, bool isGunshot)
        {
            foreach (var cache in BotCacheUtility.AllActiveBots())
            {
                var bot = cache.Bot;
                if (bot == null || bot.IsDead || bot.ProfileId == sourceId)
                    continue;

                var dist = Vector3.Distance(bot.Position, location);
                if (dist > DefaultHearingRadius)
                    continue;

                // Optional: escalate, fallback, or log sound zone here
                cache.RegisterHeardSound(location);

                if (isGunshot)
                    cache.GroupComms?.SaySuppression();
                else
                    cache.GroupComms?.SayFallback(); // footsteps → fallback
            }
        }
    }
}