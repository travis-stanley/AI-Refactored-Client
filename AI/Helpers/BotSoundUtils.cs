#nullable enable

using EFT;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Provides helper functions for detecting recent sound activity like gunfire or footsteps
    /// from nearby AI or player entities. Used by the bot hearing system for auditory awareness.
    /// </summary>
    public static class BotSoundUtils
    {
        /// <summary>
        /// Returns true if the specified player fired a weapon recently.
        /// Relies on timestamps recorded in the BotSoundRegistry.
        /// </summary>
        /// <param name="player">Player to check.</param>
        /// <param name="recentThreshold">Seconds to look back for recent fire.</param>
        /// <param name="now">Optional current time override (use Time.time by default).</param>
        public static bool DidFireRecently(Player? player, float recentThreshold = 1.5f, float now = -1f)
        {
            if (player == null || player.IsYourPlayer || !player.IsAI || string.IsNullOrEmpty(player.ProfileId))
                return false;

            return BotSoundRegistry.FiredRecently(player, recentThreshold, now);
        }

        /// <summary>
        /// Returns true if the specified player made a footstep sound recently.
        /// Relies on timestamps recorded in the BotSoundRegistry.
        /// </summary>
        /// <param name="player">Player to check.</param>
        /// <param name="recentThreshold">Seconds to look back for footstep detection.</param>
        /// <param name="now">Optional current time override (use Time.time by default).</param>
        public static bool DidStepRecently(Player? player, float recentThreshold = 1.5f, float now = -1f)
        {
            if (player == null || player.IsYourPlayer || !player.IsAI || string.IsNullOrEmpty(player.ProfileId))
                return false;

            return BotSoundRegistry.SteppedRecently(player, recentThreshold, now);
        }
    }
}
