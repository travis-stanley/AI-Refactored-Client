#nullable enable

using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Lightweight registry that tracks recent sound events for bots, such as gunfire and footsteps.
    /// Used to simulate bot hearing perception.
    /// </summary>
    public static class BotSoundRegistry
    {
        private static readonly Dictionary<string, float> _shotTimestamps = new(64);
        private static readonly Dictionary<string, float> _footstepTimestamps = new(64);

        /// <summary>
        /// Notify system that this bot has fired a weapon.
        /// Should be called when OnFireEvent is triggered or weapon discharges.
        /// </summary>
        public static void NotifyShot(Player? player)
        {
            if (player == null || player.IsYourPlayer || string.IsNullOrEmpty(player.ProfileId))
                return;

            _shotTimestamps[player.ProfileId] = Time.time;
        }

        /// <summary>
        /// Notify system that this bot has made a step sound.
        /// Should be called when a footstep sound plays (e.g., from PlayStepSound or NPCFootstepSoundPlayer).
        /// </summary>
        public static void NotifyStep(Player? player)
        {
            if (player == null || player.IsYourPlayer || string.IsNullOrEmpty(player.ProfileId))
                return;

            _footstepTimestamps[player.ProfileId] = Time.time;
        }

        /// <summary>
        /// Returns true if the bot fired recently (within N seconds).
        /// </summary>
        public static bool FiredRecently(Player? player, float withinSeconds = 1.5f, float now = -1f)
        {
            if (player == null || string.IsNullOrEmpty(player.ProfileId))
                return false;

            if (now < 0f)
                now = Time.time;

            return _shotTimestamps.TryGetValue(player.ProfileId, out float time) && now - time <= withinSeconds;
        }

        /// <summary>
        /// Returns true if the bot stepped recently (within N seconds).
        /// </summary>
        public static bool SteppedRecently(Player? player, float withinSeconds = 1.2f, float now = -1f)
        {
            if (player == null || string.IsNullOrEmpty(player.ProfileId))
                return false;

            if (now < 0f)
                now = Time.time;

            return _footstepTimestamps.TryGetValue(player.ProfileId, out float time) && now - time <= withinSeconds;
        }

        /// <summary>
        /// Reset all tracked sound activity (typically on game reset or raid end).
        /// </summary>
        public static void Clear()
        {
            _shotTimestamps.Clear();
            _footstepTimestamps.Clear();
        }
    }
}
