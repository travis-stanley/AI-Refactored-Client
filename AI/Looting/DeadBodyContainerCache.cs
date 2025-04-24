#nullable enable

using EFT;
using EFT.Interactive;
using System.Collections.Generic;

namespace AIRefactored.AI.Looting
{
    /// <summary>
    /// Caches LootableContainer references for dead player bodies to avoid expensive runtime lookups.
    /// Safe for repeated reads by AI loot systems.
    /// </summary>
    public static class DeadBodyContainerCache
    {
        #region Internal State

        private static readonly Dictionary<string, LootableContainer> _containers = new Dictionary<string, LootableContainer>(64);

        #endregion

        #region Public API

        /// <summary>
        /// Registers a dead body container for the given player, using their profile ID as the key.
        /// </summary>
        /// <param name="player">The dead player.</param>
        /// <param name="container">The associated lootable container.</param>
        public static void Register(Player? player, LootableContainer? container)
        {
            if (player == null || container == null)
                return;

            string? profileId = player.ProfileId;
            if (string.IsNullOrWhiteSpace(profileId))
                return;

            profileId = profileId.Trim();
            if (!_containers.ContainsKey(profileId))
                _containers[profileId] = container;
        }

        /// <summary>
        /// Attempts to retrieve a corpse loot container by profile ID.
        /// </summary>
        /// <param name="profileId">The bot's profile ID.</param>
        /// <returns>The associated lootable container, or null if not found.</returns>
        public static LootableContainer? Get(string? profileId)
        {
            if (profileId == null)
                return null;

            string trimmed = profileId.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return null;

            LootableContainer container;
            return _containers.TryGetValue(trimmed, out container) ? container : null;
        }

        /// <summary>
        /// Returns true if the specified profile ID has a registered corpse container.
        /// </summary>
        /// <param name="profileId">The bot's profile ID.</param>
        /// <returns>True if a corpse container is cached for the given profile ID.</returns>
        public static bool Contains(string? profileId)
        {
            if (profileId == null)
                return false;

            string trimmed = profileId.Trim();
            if (string.IsNullOrEmpty(trimmed))
                return false;

            return _containers.ContainsKey(trimmed);
        }

        /// <summary>
        /// Clears the cache of all dead body container references.
        /// </summary>
        public static void Clear()
        {
            _containers.Clear();
        }




        #endregion
    }
}
