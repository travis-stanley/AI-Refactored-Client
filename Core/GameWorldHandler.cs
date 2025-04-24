#nullable enable

using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Looting;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Threads;
using AIRefactored.Bootstrap;
using AIRefactored.Runtime;
using BepInEx.Logging;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Provides access to ClientGameWorld bots, squad checks, map info, and runtime bootstrap systems.
    /// Fully player-independent. BotBrain is strictly limited to AI players only.
    /// </summary>
    public static class GameWorldHandler
    {
        #region Fields

        private static GameObject? _bootstrapHost;
        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;
        private static readonly HashSet<int> KnownDeadBotIds = new(64);
        private static float _lastCleanupTime = -999f;
        private static bool _hasBootstrapped;
        private static bool _hasWarnedNoWorld;

        private const float DeadCleanupInterval = 10f;

        private static ClientGameWorld? CachedWorld =>
            Singleton<ClientGameWorld>.Instantiated ? Singleton<ClientGameWorld>.Instance : null;

        #endregion

        #region Runtime Bootstrap

        public static void TryInitializeWorld()
        {
            if (_hasBootstrapped)
                return;

            var world = CachedWorld;

            if (world == null)
            {
                if (!_hasWarnedNoWorld)
                {
                    Logger.LogWarning("[GameWorldHandler] ⚠ GameWorld is null — host may not have finished loading.");
                    _hasWarnedNoWorld = true;
                }
                return;
            }

            if (world.AllAlivePlayersList == null)
            {
                if (!_hasWarnedNoWorld)
                {
                    Logger.LogWarning("[GameWorldHandler] ⚠ Player list is null — world not ready.");
                    _hasWarnedNoWorld = true;
                }
                return;
            }

            _hasWarnedNoWorld = false;

            HookBotSpawns();
            _hasBootstrapped = true;
        }

        public static void HookBotSpawns()
        {
            if (_bootstrapHost != null)
                return;

            _bootstrapHost = new GameObject("AIRefactored.BootstrapHost");
            Object.DontDestroyOnLoad(_bootstrapHost);

            _bootstrapHost.AddComponent<WorldBootstrapper>();
            if (FikaHeadlessDetector.IsHeadless)
                _bootstrapHost.AddComponent<BotWorkGroupDispatcher>();

            Logger.LogInfo("[AIRefactored] ✅ GameWorldHandler initialized.");
        }

        public static void UnhookBotSpawns()
        {
            if (_bootstrapHost != null)
            {
                Object.Destroy(_bootstrapHost);
                _bootstrapHost = null;
            }

            HotspotRegistry.Clear();
            LootRegistry.Clear();
            _hasBootstrapped = false;
            _hasWarnedNoWorld = false;

            Logger.LogInfo("[GameWorldHandler] 🔻 GameWorldHandler shut down.");
        }

        #endregion

        #region Bot AI Injection

        public static void TryAttachBotBrain(BotOwner bot)
        {
            var player = bot.GetPlayer;
            if (player == null || !player.IsAI || bot.IsDead)
                return;

            if (player.gameObject == null)
                return;

            BotBrainGuardian.Enforce(player.gameObject);

            if (player.gameObject.GetComponent<BotBrain>() != null)
                return;

            var brain = player.gameObject.AddComponent<BotBrain>();
            brain.Initialize(bot);
        }

        public static void EnforceBotBrains()
        {
            var world = CachedWorld;
            if (world?.AllAlivePlayersList == null)
                return;

            for (int i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                var player = world.AllAlivePlayersList[i];
                if (player != null && player.IsAI && player.gameObject != null)
                {
                    BotBrainGuardian.Enforce(player.gameObject);
                }
            }
        }

        #endregion

        #region Death Cleanup

        public static void CleanupDeadBotsSmoothly()
        {
            if (Time.time - _lastCleanupTime < DeadCleanupInterval)
                return;

            _lastCleanupTime = Time.time;
            var world = CachedWorld;
            if (world?.AllAlivePlayersList == null)
                return;

            for (int i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                var player = world.AllAlivePlayersList[i];
                if (player == null || !player.IsAI)
                    continue;

                if (player.HealthController?.IsAlive == false)
                {
                    int id = player.GetInstanceID();
                    if (!KnownDeadBotIds.Contains(id))
                    {
                        KnownDeadBotIds.Add(id);

                        if (player.gameObject != null)
                        {
                            player.gameObject.SetActive(false);
                            Object.Destroy(player.gameObject, 3f);
                        }

                        Logger.LogDebug($"[GameWorldHandler] 🧹 Cleaned up dead bot {player.Profile?.Info?.Nickname ?? "Unknown"}.");
                    }
                }
            }
        }

        #endregion

        #region Map Name Resolution (LocationSettingsClass)

        public static string GetCurrentMapName()
        {
            var locationSettings = Singleton<LocationSettingsClass>.Instance;

            if (locationSettings != null && locationSettings.locations != null)
            {
                foreach (var kvp in locationSettings.locations)
                {
                    if (kvp.Value.Enabled &&
                        LocationSettingsClass.Location.AvailableMaps.Contains(kvp.Key))
                    {
                        return kvp.Key.ToLowerInvariant(); // Scene-safe ID
                    }
                }
            }

            return "unknown";
        }

        #endregion

        #region Player Accessors

        public static ClientGameWorld? Get() => CachedWorld;

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

        public static List<Player> GetAllHumanPlayers()
        {
            var result = new List<Player>();
            var world = CachedWorld;
            if (world?.AllAlivePlayersList == null)
                return result;

            for (int i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                var p = world.AllAlivePlayersList[i];
                if (p != null && p.AIData == null && p.HealthController?.IsAlive == true)
                    result.Add(p);
            }

            return result;
        }

        public static float GetNearestHumanPlayerDistance(Vector3 position)
        {
            float closest = float.MaxValue;
            var humans = GetAllHumanPlayers();

            for (int i = 0; i < humans.Count; i++)
            {
                float dist = Vector3.Distance(humans[i].Position, position);
                if (dist < closest)
                    closest = dist;
            }

            return closest;
        }

        public static bool IsNearRealPlayer(Vector3 position, float radius)
        {
            var humans = GetAllHumanPlayers();
            for (int i = 0; i < humans.Count; i++)
            {
                if (Vector3.Distance(humans[i].Position, position) <= radius)
                    return true;
            }

            return false;
        }

        public static bool IsNearTeammate(Vector3 position, float radius, string? groupId = null)
        {
            if (string.IsNullOrEmpty(groupId))
                return false;

            var world = CachedWorld;
            if (world?.AllAlivePlayersList == null)
                return false;

            for (int i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                var p = world.AllAlivePlayersList[i];
                if (p == null || !p.IsAI || p.HealthController?.IsAlive != true)
                    continue;

                if (p.Profile?.Info?.GroupId == groupId &&
                    Vector3.Distance(p.Position, position) <= radius)
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region System Refresh

        public static void RefreshLootRegistry()
        {
            Logger.LogInfo("[GameWorldHandler] 🔄 Refreshing loot registries...");
            LootRegistry.Clear();
            LootBootstrapper.RegisterAllLoot();
            BotDeadBodyScanner.ScanAll();
        }

        #endregion

        #region State Access

        public static bool IsInitialized => _hasBootstrapped;

        #endregion
    }
}
