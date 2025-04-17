#nullable enable

using System.Collections.Generic;
using EFT;
using Comfort.Common;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Provides safe access to the active <see cref="ClientGameWorld"/>, map metadata,
    /// main player position, alive players list, and team proximity checks.
    /// Supports both local play and FIKA/Coop multiplayer modes.
    /// </summary>
    public static class GameWorldHandler
    {
        #region Cache

        private static Vector3 _cachedPlayerPosition = Vector3.zero;
        private static float _lastUpdateTime = -1f;
        private const float CacheRefreshRate = 0.1f;

        /// <summary>
        /// Retrieves the current instantiated ClientGameWorld instance (if available).
        /// </summary>
        private static ClientGameWorld? CachedWorld =>
            Singleton<ClientGameWorld>.Instantiated
                ? Singleton<ClientGameWorld>.Instance
                : null;

        #endregion

        #region World Access

        /// <summary>
        /// Returns the current <see cref="ClientGameWorld"/> if available.
        /// </summary>
        public static ClientGameWorld? Get() => CachedWorld;

        /// <summary>
        /// Gets the current map's internal name or "unknown" if unavailable.
        /// </summary>
        public static string GetCurrentMapName()
        {
            return CachedWorld?.MainPlayer?.Location ?? "unknown";
        }

        #endregion

        #region Main Player Access

        /// <summary>
        /// Attempts to retrieve the main player's current position with default caching interval.
        /// </summary>
        /// <param name="position">Out parameter to hold the player’s position.</param>
        /// <returns>True if the main player was found and alive; otherwise, false.</returns>
        public static bool TryGetMainPlayerPosition(out Vector3 position)
        {
            return TryGetMainPlayerPosition(out position, CacheRefreshRate);
        }

        /// <summary>
        /// Attempts to retrieve the main player's position with a custom refresh rate for caching.
        /// </summary>
        /// <param name="position">Out parameter for the player’s position.</param>
        /// <param name="refreshRate">Minimum seconds between cache updates.</param>
        /// <returns>True if the main player is alive and position was updated or retrieved from cache.</returns>
        public static bool TryGetMainPlayerPosition(out Vector3 position, float refreshRate)
        {
            position = Vector3.zero;

            var player = CachedWorld?.MainPlayer;
            if (player == null || !player.HealthController.IsAlive)
                return false;

            if (Time.time - _lastUpdateTime > refreshRate)
            {
                _cachedPlayerPosition = player.Transform.position;
                _lastUpdateTime = Time.time;
            }

            position = _cachedPlayerPosition;
            return true;
        }

        /// <summary>
        /// Calculates distance from the given position to the main player.
        /// Returns float.MaxValue if the player is not found.
        /// </summary>
        public static float DistanceToMainPlayer(Vector3 worldPos)
        {
            return TryGetMainPlayerPosition(out var mainPos)
                ? Vector3.Distance(worldPos, mainPos)
                : float.MaxValue;
        }

        /// <summary>
        /// Returns true if the given world position is within a certain range of the main player.
        /// </summary>
        public static bool IsWithinPlayerRange(Vector3 position, float range)
        {
            return DistanceToMainPlayer(position) <= range;
        }

        #endregion

        #region World Players

        /// <summary>
        /// Retrieves a list of all currently alive players in the ClientGameWorld.
        /// Includes both AI and human players.
        /// </summary>
        public static List<Player> GetAllAlivePlayers()
        {
            var players = new List<Player>();
            var world = CachedWorld;

            if (world?.AllAlivePlayersList == null)
                return players;

            foreach (var p in world.AllAlivePlayersList)
            {
                if (p != null && p.HealthController?.IsAlive == true)
                    players.Add(p);
            }

            return players;
        }

        #endregion

        #region Teammate Proximity

        /// <summary>
        /// Returns true if any alive player in the specified group is within the given radius of a position.
        /// </summary>
        /// <param name="position">Target world position to test.</param>
        /// <param name="radius">Distance radius to check.</param>
        /// <param name="groupId">Optional group identifier (null-safe).</param>
        public static bool IsNearTeammate(Vector3 position, float radius, string? groupId = null)
        {
            if (string.IsNullOrEmpty(groupId))
                return false;

            foreach (var player in GetAllAlivePlayers())
            {
                var info = player?.Profile?.Info;
                if (info != null && info.GroupId == groupId)
                {
                    if (Vector3.Distance(player.Transform.position, position) <= radius)
                        return true;
                }
            }

            return false;
        }

        #endregion
    }
}
