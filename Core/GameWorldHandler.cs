#nullable enable

using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Threads;
using AIRefactored.Runtime;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Provides safe access to ClientGameWorld data: player location, map name, and squad proximity.
    /// Manages runtime AIRefactored bootstrap, bot brain enforcement, and world-level system initialization.
    /// </summary>
    public static class GameWorldHandler
    {
        #region Fields

        private static Vector3 _cachedPlayerPosition = Vector3.zero;
        private static float _lastUpdateTime = -1f;
        private const float CacheRefreshRate = 0.1f;

        private static GameObject? _bootstrapHost;
        private static readonly bool _debug = false;

        private static ManualLogSource Logger => AIRefactoredController.Logger;

        private static ClientGameWorld? CachedWorld =>
            Singleton<ClientGameWorld>.Instantiated ? Singleton<ClientGameWorld>.Instance : null;

        #endregion

        #region Runtime Initialization

        /// <summary>
        /// Begins watching for world load and initializes world-level AI systems.
        /// </summary>
        public static void HookBotSpawns()
        {
            if (_bootstrapHost != null)
                return;

            _bootstrapHost = new GameObject("AIRefactored.BootstrapHost");
            _bootstrapHost.AddComponent<WorldBootstrapper>();
            GameObject.DontDestroyOnLoad(_bootstrapHost);

            Logger.LogInfo("[AIRefactored] ✅ GameWorldHandler initialized.");
        }

        /// <summary>
        /// Removes world-level hooks and cleanup logic.
        /// </summary>
        public static void UnhookBotSpawns()
        {
            if (_bootstrapHost != null)
            {
                GameObject.Destroy(_bootstrapHost);
                _bootstrapHost = null;
                HotspotLoader.Reset();

                Logger.LogInfo("[AIRefactored] 🔻 GameWorldHandler shut down.");
            }
        }

        #endregion

        #region Game World Accessors

        public static ClientGameWorld? Get() => CachedWorld;

        public static string GetCurrentMapName()
        {
            if (CachedWorld?.MainPlayer == null)
                return "unknown";

            return CachedWorld.MainPlayer.Location ?? "unknown";
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
            var result = new List<Player>();
            var world = CachedWorld;
            if (world?.AllAlivePlayersList == null)
                return result;

            for (int i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                var p = world.AllAlivePlayersList[i];
                if (p != null && p.HealthController?.IsAlive == true)
                    result.Add(p);
            }

            return result;
        }

        public static bool IsNearRealPlayer(Vector3 position, float radius)
        {
            var players = GetAllAlivePlayers();
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!p.IsAI && Vector3.Distance(p.Position, position) <= radius)
                    return true;
            }

            return false;
        }

        public static bool IsNearTeammate(Vector3 position, float radius, string? groupId = null)
        {
            if (string.IsNullOrEmpty(groupId))
                return false;

            var players = GetAllAlivePlayers();
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p.Profile?.Info?.GroupId == groupId &&
                    Vector3.Distance(p.Transform.position, position) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Embedded MonoBootstrapper

        private class WorldBootstrapper : MonoBehaviour
        {
            private float _lastSweepTime = -999f;
            private const float SweepInterval = 20f;

            private void Awake()
            {
                StartCoroutine(WatchForGameWorld());
            }

            private IEnumerator WatchForGameWorld()
            {
                while (Singleton<BotSpawner>.Instance == null || Singleton<GameWorld>.Instance == null)
                    yield return null;

                Singleton<BotSpawner>.Instance.OnBotCreated += HandleBotCreated;

                Logger.LogInfo("[AIRefactored] 🧠 World bootstrapper now listening for bots.");

                InitializeWorldLevelSystems();
            }

            private void OnDestroy()
            {
                if (Singleton<BotSpawner>.Instantiated)
                    Singleton<BotSpawner>.Instance.OnBotCreated -= HandleBotCreated;
            }

            private void Update()
            {
                if (Time.time - _lastSweepTime > SweepInterval)
                {
                    EnforceBotBrains();
                    _lastSweepTime = Time.time;
                }
            }

            private void HandleBotCreated(BotOwner bot)
            {
                var player = bot.GetPlayer;
                if (player == null || !player.IsAI || player.IsYourPlayer)
                    return;

                BotBrainGuardian.Enforce(player.gameObject);
            }

            private void EnforceBotBrains()
            {
                var world = Get();
                if (world?.AllAlivePlayersList == null)
                    return;

                for (int i = 0; i < world.AllAlivePlayersList.Count; i++)
                {
                    var player = world.AllAlivePlayersList[i];
                    if (player != null && player.IsAI && !player.IsYourPlayer)
                        BotBrainGuardian.Enforce(player.gameObject);
                }
            }

            /// <summary>
            /// Initializes all world-level AIRefactored systems once the map is fully loaded.
            /// </summary>
            private void InitializeWorldLevelSystems()
            {
                HotspotLoader.Reset();
                HotspotLoader.LoadCurrentMap();

                Logger.LogInfo("[AIRefactored] 🌐 World-level AI systems initialized.");
            }
        }

        #endregion
    }
}
