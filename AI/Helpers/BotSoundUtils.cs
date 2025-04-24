#nullable enable

using EFT;

namespace AIRefactored.AI.Helpers
{
    /// <summary>
    /// Sound-based awareness utilities for bots.
    /// Filters audio events based on recency and friend-or-foe logic.
    /// </summary>
    public static class BotSoundUtils
    {
        /// <summary>
        /// Determines if a non-teammate fired recently (e.g., gunshot detection).
        /// </summary>
        public static bool DidFireRecently(BotOwner self, Player? player, float recentThreshold = 1.5f, float now = -1f)
        {
            return IsValidSoundSource(self, player) &&
                   BotSoundRegistry.FiredRecently(player, recentThreshold, now);
        }

        /// <summary>
        /// Determines if a non-teammate stepped recently (e.g., footstep detection).
        /// </summary>
        public static bool DidStepRecently(BotOwner self, Player? player, float recentThreshold = 1.2f, float now = -1f)
        {
            return IsValidSoundSource(self, player) &&
                   BotSoundRegistry.SteppedRecently(player, recentThreshold, now);
        }

        /// <summary>
        /// Returns true if the sound-emitting player is a valid target (not self or teammate, and not a human).
        /// </summary>
        private static bool IsValidSoundSource(BotOwner self, Player? player)
        {
            if (player == null || player.AIData == null || self.GetPlayer == null)
                return false;

            if (player == self.GetPlayer)
                return false;

            string? selfGroupId = self.Profile?.Info?.GroupId;
            string? sourceGroupId = player.Profile?.Info?.GroupId;

            if (!string.IsNullOrEmpty(selfGroupId) && selfGroupId == sourceGroupId)
                return false;

            return true;
        }
    }
}
