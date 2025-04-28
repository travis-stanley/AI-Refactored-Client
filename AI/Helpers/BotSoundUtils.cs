#nullable enable

namespace AIRefactored.AI.Helpers
{
    using EFT;

    /// <summary>
    ///     Sound-based awareness utilities for bots.
    ///     Filters audio events based on recency and friend-or-foe logic.
    /// </summary>
    public static class BotSoundUtils
    {
        /// <summary>
        ///     Returns true if a non-teammate fired recently.
        /// </summary>
        public static bool DidFireRecently(BotOwner self, Player? source, float recentThreshold = 1.5f, float now = -1f)
        {
            return IsValidSoundSource(self, source) && BotSoundRegistry.FiredRecently(source, recentThreshold, now);
        }

        /// <summary>
        ///     Returns true if a non-teammate stepped recently.
        /// </summary>
        public static bool DidStepRecently(BotOwner self, Player? source, float recentThreshold = 1.2f, float now = -1f)
        {
            return IsValidSoundSource(self, source) && BotSoundRegistry.SteppedRecently(source, recentThreshold, now);
        }

        /// <summary>
        ///     Filters out invalid sound sources: null, same player, same group, or invalid profile.
        /// </summary>
        private static bool IsValidSoundSource(BotOwner self, Player? source)
        {
            if (self == null || self.GetPlayer == null || source == null || source.AIData == null)
                return false;

            if (source == self.GetPlayer)
                return false;

            var selfGroup = self.Profile?.Info?.GroupId;
            var sourceGroup = source.Profile?.Info?.GroupId;

            if (!string.IsNullOrEmpty(selfGroup) && selfGroup == sourceGroup)
                return false;

            // Optional: ignore human players if needed
            // if (!source.IsAI)
            // return false;
            return true;
        }
    }
}