#nullable enable

using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Runtime registry for tracking gunfire and footsteps from bots and players.
    /// Used by AI hearing systems to simulate awareness of sound events.
    /// </summary>
    public static class BotSoundRegistry
    {
        #region Internal Data

        private static readonly Dictionary<string, float> _shotTimestamps = new Dictionary<string, float>(64);
        private static readonly Dictionary<string, float> _footstepTimestamps = new Dictionary<string, float>(64);

        #endregion

        #region Notification API

        /// <summary>
        /// Records that a shot has been fired by the given player.
        /// </summary>
        /// <param name="player">The player who fired the weapon.</param>
        public static void NotifyShot(Player? player)
        {
            if (player == null || string.IsNullOrEmpty(player.ProfileId) || player.IsYourPlayer)
                return;

            _shotTimestamps[player.ProfileId] = Time.time;
        }

        /// <summary>
        /// Records that a footstep sound occurred for the given player.
        /// </summary>
        /// <param name="player">The player who stepped.</param>
        public static void NotifyStep(Player? player)
        {
            if (player == null || string.IsNullOrEmpty(player.ProfileId) || player.IsYourPlayer)
                return;

            _footstepTimestamps[player.ProfileId] = Time.time;
        }

        #endregion

        #region Query API

        /// <summary>
        /// Returns true if the given player fired recently (within the specified duration).
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="withinSeconds">Time window for "recent" in seconds.</param>
        /// <param name="now">Current time, defaults to Time.time.</param>
        /// <returns>True if shot occurred within the time window.</returns>
        public static bool FiredRecently(Player? player, float withinSeconds = 1.5f, float now = -1f)
        {
            if (player == null || string.IsNullOrEmpty(player.ProfileId))
                return false;

            if (now < 0f)
                now = Time.time;

            return _shotTimestamps.TryGetValue(player.ProfileId, out float shotTime) &&
                   (now - shotTime) <= withinSeconds;
        }

        /// <summary>
        /// Returns true if the given player stepped recently (within the specified duration).
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="withinSeconds">Time window for "recent" in seconds.</param>
        /// <param name="now">Current time, defaults to Time.time.</param>
        /// <returns>True if step occurred within the time window.</returns>
        public static bool SteppedRecently(Player? player, float withinSeconds = 1.2f, float now = -1f)
        {
            if (player == null || string.IsNullOrEmpty(player.ProfileId))
                return false;

            if (now < 0f)
                now = Time.time;

            return _footstepTimestamps.TryGetValue(player.ProfileId, out float stepTime) &&
                   (now - stepTime) <= withinSeconds;
        }

        #endregion

        #region Maintenance

        /// <summary>
        /// Clears all tracked shot and footstep data. Call on session reset or cleanup.
        /// </summary>
        public static void Clear()
        {
            _shotTimestamps.Clear();
            _footstepTimestamps.Clear();
        }

        #endregion
    }
}
