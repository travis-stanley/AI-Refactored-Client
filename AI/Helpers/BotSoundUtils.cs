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
        public static bool DidFireRecently(Player player, float recentThreshold = 1.5f)
        {
            if (player == null || !player.IsAI || player.IsYourPlayer)
                return false;

            // Use our registry tracking for firing events
            return BotSoundRegistry.FiredRecently(player, recentThreshold);
        }

        /// <summary>
        /// Checks if the specified player has stepped recently.
        /// Uses the BotSoundRegistry hook if available.
        /// </summary>
        public static bool DidStepRecently(Player player, float recentThreshold = 1.5f)
        {
            if (player == null || !player.IsAI || player.IsYourPlayer)
                return false;

            return BotSoundRegistry.SteppedRecently(player, recentThreshold);
        }
    }
}
