#nullable enable

namespace AIRefactored.AI.Looting
{
    using System.Collections.Generic;

    using EFT;
    using EFT.Interactive;

    /// <summary>
    ///     Caches LootableContainer references for dead player bodies to avoid expensive runtime lookups.
    ///     Safe for repeated reads by AI loot systems.
    /// </summary>
    public static class DeadBodyContainerCache
    {
        /// <summary>
        ///     Internal dictionary mapping profile IDs to lootable corpse containers.
        /// </summary>
        private static readonly Dictionary<string, LootableContainer> _containers = new(64);

        /// <summary>
        ///     Clears the cache of all dead body container references.
        /// </summary>
        public static void Clear()
        {
            _containers.Clear();
        }

        /// <summary>
        ///     Returns true if the specified profile ID has a registered corpse container.
        /// </summary>
        /// <param name="profileId">The bot's profile ID.</param>
        /// <returns>True if a corpse container is cached for the given profile ID.</returns>
        public static bool Contains(string? profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            var trimmedId = profileId!.Trim();
            return _containers.ContainsKey(trimmedId);
        }

        /// <summary>
        ///     Retrieves the cached loot container for the specified profile ID, if available.
        /// </summary>
        /// <param name="profileId">The profile ID to query.</param>
        /// <returns>The lootable container if registered, or null otherwise.</returns>
        /// <summary>
        ///     Retrieves the cached loot container for the specified profile ID, if available.
        /// </summary>
        /// <param name="profileId">The profile ID to query.</param>
        /// <returns>The lootable container if registered, or null otherwise.</returns>
        /// <summary>
        ///     Attempts to retrieve a corpse loot container by profile ID.
        /// </summary>
        /// <param name="profileId">The bot's profile ID.</param>
        /// <returns>The associated lootable container, or null if not found.</returns>
        public static LootableContainer? Get(string? profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return null;

            var trimmedId = profileId!.Trim();
            return _containers.TryGetValue(trimmedId, out var container) ? container : null;
        }

        /// <summary>
        ///     Registers a lootable corpse container using the given player's profile ID.
        /// </summary>
        /// <param name="player">The player who died.</param>
        /// <param name="container">The corpse's lootable container.</param>
        public static void Register(Player? player, LootableContainer? container)
        {
            if (player == null || container == null)
                return;

            var profileId = player.ProfileId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(profileId) || _containers.ContainsKey(profileId))
                return;

            _containers[profileId] = container;
        }
    }
}