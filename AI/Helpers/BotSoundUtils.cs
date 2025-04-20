#nullable enable

using EFT;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Utility class for detecting sound events like gunfire or footstep activity from bots or players.
    /// Integrates with direct sound hooks and falls back to local estimation if needed.
    /// </summary>
    public static class BotSoundUtils
    {
        /// <summary>
        /// Checks if the specified player has fired recently.
        /// Uses the BotSoundRegistry hook if available.
        /// </summary>
        public static bool DidFireRecently(Player? player, float recentThreshold = 1.5f, float now = -1f)
        {
            if (player == null || !player.IsAI || player.IsYourPlayer || string.IsNullOrEmpty(player.ProfileId))
                return false;

            return BotSoundRegistry.FiredRecently(player, recentThreshold, now);
        }

        /// <summary>
        /// Checks if the specified player has stepped recently.
        /// Uses the BotSoundRegistry hook if available.
        /// </summary>
        public static bool DidStepRecently(Player? player, float recentThreshold = 1.5f, float now = -1f)
        {
            if (player == null || !player.IsAI || player.IsYourPlayer || string.IsNullOrEmpty(player.ProfileId))
                return false;

            return BotSoundRegistry.SteppedRecently(player, recentThreshold, now);
        }
    }
}
