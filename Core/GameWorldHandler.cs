#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Provides safe access to ClientGameWorld data: player location, map name, and squad proximity.
    /// Supports both local and multiplayer (e.g., FIKA) contexts.
    /// </summary>
    public static class GameWorldHandler
    {
        #region Fields

        private static Vector3 _cachedPlayerPosition = Vector3.zero;
        private static float _lastUpdateTime = -1f;
        private const float CacheRefreshRate = 0.1f;
        private static readonly bool _debug = false;

        private static ManualLogSource Logger => AIRefactoredController.Logger;

        private static ClientGameWorld? CachedWorld =>
            Singleton<ClientGameWorld>.Instantiated ? Singleton<ClientGameWorld>.Instance : null;

        #endregion

        #region Public API

        public static ClientGameWorld? Get() => CachedWorld;

        public static string GetCurrentMapName()
        {
            var name = CachedWorld?.MainPlayer?.Location ?? "unknown";
            if (_debug)
                Logger.LogDebug($"[GameWorldHandler] Current map: {name}");
            return name;
        }

        public static bool TryGetMainPlayerPosition(out Vector3 position, float refreshRate = CacheRefreshRate)
        {
            position = Vector3.zero;

            var player = CachedWorld?.MainPlayer;
            if (player == null || !player.HealthController.IsAlive)
                return false;

            if (Time.time - _lastUpdateTime > refreshRate)
            {
                _cachedPlayerPosition = player.Transform.position;
                _lastUpdateTime = Time.time;

                if (_debug)
                    Logger.LogDebug($"[GameWorldHandler] Cached main player position: {_cachedPlayerPosition}");
            }

            position = _cachedPlayerPosition;
            return true;
        }

        public static float DistanceToMainPlayer(Vector3 worldPos) =>
            TryGetMainPlayerPosition(out var mainPos) ? Vector3.Distance(worldPos, mainPos) : float.MaxValue;

        public static bool IsWithinPlayerRange(Vector3 position, float range) =>
            DistanceToMainPlayer(position) <= range;

        public static List<Player> GetAllAlivePlayers()
        {
            var list = new List<Player>();
            var all = CachedWorld?.AllAlivePlayersList;
            if (all == null) return list;

            foreach (var p in all)
            {
                if (p?.HealthController?.IsAlive == true)
                    list.Add(p);
            }

            return list;
        }

        public static bool IsNearRealPlayer(Vector3 position, float radius)
        {
            foreach (var p in GetAllAlivePlayers())
            {
                if (!p.IsAI && Vector3.Distance(p.Position, position) <= radius)
                    return true;
            }

            return false;
        }

        public static bool IsNearTeammate(Vector3 position, float radius, string? groupId = null)
        {
            if (string.IsNullOrEmpty(groupId))
                return false;

            foreach (var player in GetAllAlivePlayers())
            {
                if (player?.Profile?.Info?.GroupId == groupId &&
                    Vector3.Distance(player.Transform.position, position) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
