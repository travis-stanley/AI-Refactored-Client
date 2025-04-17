#nullable enable

using System.Collections.Generic;
using EFT;
using Comfort.Common;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Utility for safe access to the current GameWorld, map name, player proximity, and teammate logic.
    /// Supports both local and Coop/FIKA multiplayer compatibility.
    /// </summary>
    public static class GameWorldHandler
    {
        private static Vector3 _cachedPlayerPosition = Vector3.zero;
        private static float _lastUpdateTime = -1f;
        private const float CacheRefreshRate = 0.1f;

        private static ClientGameWorld? CachedWorld => Singleton<ClientGameWorld>.Instantiated ? Singleton<ClientGameWorld>.Instance : null;

        /// <summary>
        /// Gets the current ClientGameWorld if available.
        /// </summary>
        public static ClientGameWorld? Get() => CachedWorld;

        /// <summary>
        /// Returns the current map identifier or "unknown".
        /// </summary>
        public static string GetCurrentMapName()
        {
            return CachedWorld?.MainPlayer?.Location ?? "unknown";
        }

        /// <summary>
        /// Tries to get the main player’s position (cached for performance).
        /// </summary>
        public static bool TryGetMainPlayerPosition(out Vector3 position)
        {
            return TryGetMainPlayerPosition(out position, CacheRefreshRate);
        }

        /// <summary>
        /// Tries to get the main player's position, optionally bypassing cache delay with a custom refresh interval.
        /// </summary>
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
        /// Returns distance to the main player or float.MaxValue if not found.
        /// </summary>
        public static float DistanceToMainPlayer(Vector3 worldPos)
        {
            return TryGetMainPlayerPosition(out var mainPos)
                ? Vector3.Distance(worldPos, mainPos)
                : float.MaxValue;
        }

        /// <summary>
        /// Checks if the given world position is within a range of the main player.
        /// </summary>
        public static bool IsWithinPlayerRange(Vector3 position, float range)
        {
            return DistanceToMainPlayer(position) <= range;
        }

        /// <summary>
        /// Returns a list of all alive players in the current game world.
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

        /// <summary>
        /// Checks if any player in the given group is within a radius of the target position.
        /// </summary>
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
    }
}
