#nullable enable

using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Central registry for gunfire and footstep timestamps.
    /// Bots use this for realistic hearing and directional threat modeling.
    /// </summary>
    public static class BotSoundRegistry
    {
        #region Internal State

        private static readonly Dictionary<string, float> _shotTimestamps = new Dictionary<string, float>(64);
        private static readonly Dictionary<string, float> _footstepTimestamps = new Dictionary<string, float>(64);

        #endregion

        #region Notification API

        /// <summary>
        /// Logs a gunshot for the given player if valid and AI or remote.
        /// </summary>
        public static void NotifyShot(Player? player)
        {
            if (!IsTrackable(player))
                return;

            string profileId = player!.ProfileId;
            _shotTimestamps[profileId] = Time.time;
        }

        /// <summary>
        /// Logs a footstep for the given player if valid and AI or remote.
        /// </summary>
        public static void NotifyStep(Player? player)
        {
            if (!IsTrackable(player))
                return;

            string profileId = player!.ProfileId;
            _footstepTimestamps[profileId] = Time.time;
        }

        /// <summary>
        /// Returns true if the player fired recently within a configurable time window.
        /// </summary>
        public static bool FiredRecently(Player? player, float withinSeconds = 1.5f, float now = -1f)
        {
            if (player == null || string.IsNullOrEmpty(player.ProfileId))
                return false;

            float lastShot;
            if (!_shotTimestamps.TryGetValue(player.ProfileId, out lastShot))
                return false;

            float currentTime = (now >= 0f) ? now : Time.time;
            return (currentTime - lastShot) <= withinSeconds;
        }

        /// <summary>
        /// Returns true if the player stepped recently within a configurable time window.
        /// </summary>
        public static bool SteppedRecently(Player? player, float withinSeconds = 1.2f, float now = -1f)
        {
            if (player == null || string.IsNullOrEmpty(player.ProfileId))
                return false;

            float lastStep;
            if (!_footstepTimestamps.TryGetValue(player.ProfileId, out lastStep))
                return false;

            float currentTime = (now >= 0f) ? now : Time.time;
            return (currentTime - lastStep) <= withinSeconds;
        }

        #endregion

        #region Maintenance API

        /// <summary>
        /// Clears all tracked sound data. Should be called on map unload or session reset.
        /// </summary>
        public static void Clear()
        {
            _shotTimestamps.Clear();
            _footstepTimestamps.Clear();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Determines if the player is a valid target for auditory memory.
        /// Skips your player and null cases.
        /// </summary>
        private static bool IsTrackable(Player? player)
        {
            return player != null &&
                   !player.IsYourPlayer &&
                   !string.IsNullOrEmpty(player.ProfileId);
        }

        #endregion
    }
}
