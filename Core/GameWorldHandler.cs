#nullable enable

namespace AIRefactored.Core
{
    using System.Collections.Generic;
    using System.Linq;

    using AIRefactored.AI.Hotspots;
    using AIRefactored.AI.Looting;
    using AIRefactored.AI.Optimization;
    using AIRefactored.AI.Threads;
    using AIRefactored.Bootstrap;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using Comfort.Common;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Provides access to ClientGameWorld bots, squad checks, map info, and runtime bootstrap systems.
    ///     Fully player-independent. BotBrain is strictly limited to AI players only.
    /// </summary>
    public static class GameWorldHandler
    {
        private const float DeadCleanupInterval = 10f;

        private static readonly HashSet<int> KnownDeadBotIds = new(64);

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static GameObject? _bootstrapHost;

        private static bool _hasWarnedNoWorld;

        private static float _lastCleanupTime = -999f;

        public static bool IsInitialized { get; private set; }

        private static ClientGameWorld? CachedWorld =>
            Singleton<ClientGameWorld>.Instantiated ? Singleton<ClientGameWorld>.Instance : null;

        public static void CleanupDeadBotsSmoothly()
        {
            if (Time.time - _lastCleanupTime < DeadCleanupInterval)
                return;

            _lastCleanupTime = Time.time;
            var world = CachedWorld;
            if (world?.AllAlivePlayersList == null)
                return;

            for (var i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                var player = world.AllAlivePlayersList[i];
                if (player == null || !player.IsAI)
                    continue;

                if (player.HealthController?.IsAlive == false)
                {
                    var id = player.GetInstanceID();
                    if (!KnownDeadBotIds.Contains(id))
                    {
                        KnownDeadBotIds.Add(id);

                        if (player.gameObject != null)
                        {
                            player.gameObject.SetActive(false);
                            object.Destroy(player.gameObject, 3f);
                        }

                        Logger.LogDebug(
                            $"[GameWorldHandler] 🧹 Cleaned up dead bot {player.Profile?.Info?.Nickname ?? "Unknown"}.");
                    }
                }
            }
        }

        public static void EnforceBotBrains()
        {
            var world = CachedWorld;
            if (world?.AllAlivePlayersList == null)
                return;

            for (var i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                var player = world.AllAlivePlayersList[i];
                if (player != null && player.IsAI && player.gameObject != null)
                    BotBrainGuardian.Enforce(player.gameObject);
            }
        }

        public static ClientGameWorld? Get()
        {
            return CachedWorld;
        }

        public static List<Player> GetAllAlivePlayers()
        {
            var result = new List<Player>();
            var world = CachedWorld;
            if (world?.AllAlivePlayersList == null)
                return result;

            for (var i = 0; i < world.AllAlivePlayersList.Count; i++)
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

            for (var i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                var p = world.AllAlivePlayersList[i];
                if (p != null && p.AIData == null && p.HealthController?.IsAlive == true)
                    result.Add(p);
            }

            return result;
        }

        /// <summary>
        ///     Returns the current EFT location ID (e.g. 'factory4_day', 'customs', etc).
        /// </summary>
        public static string GetCurrentMapName()
        {
            var locationSettings = Singleton<LocationSettingsClass>.Instance;

            if (locationSettings != null && locationSettings.locations != null)
                foreach (var kvp in locationSettings.locations)
                    if (kvp.Value.Enabled && LocationSettingsClass.Location.AvailableMaps.Contains(kvp.Key))
                        return kvp.Key.ToLowerInvariant(); // Scene-safe ID

            return "unknown";
        }

        public static float GetNearestHumanPlayerDistance(Vector3 position)
        {
            var closest = float.MaxValue;
            var humans = GetAllHumanPlayers();

            for (var i = 0; i < humans.Count; i++)
            {
                var dist = Vector3.Distance(humans[i].Position, position);
                if (dist < closest)
                    closest = dist;
            }

            return closest;
        }

        public static void HookBotSpawns()
        {
            if (_bootstrapHost != null)
                return;

            _bootstrapHost = new GameObject("AIRefactored.BootstrapHost");
            object.DontDestroyOnLoad(_bootstrapHost);

            _bootstrapHost.AddComponent<WorldBootstrapper>();
            if (FikaHeadlessDetector.IsHeadless)
                _bootstrapHost.AddComponent<BotWorkGroupDispatcher>();

            Logger.LogInfo("[AIRefactored] ✅ GameWorldHandler initialized.");
        }

        public static bool IsNearRealPlayer(Vector3 position, float radius)
        {
            var humans = GetAllHumanPlayers();
            for (var i = 0; i < humans.Count; i++)
                if (Vector3.Distance(humans[i].Position, position) <= radius)
                    return true;

            return false;
        }

        public static bool IsNearTeammate(Vector3 position, float radius, string? groupId = null)
        {
            if (string.IsNullOrEmpty(groupId))
                return false;

            var world = CachedWorld;
            if (world?.AllAlivePlayersList == null)
                return false;

            for (var i = 0; i < world.AllAlivePlayersList.Count; i++)
            {
                var p = world.AllAlivePlayersList[i];
                if (p == null || !p.IsAI || p.HealthController?.IsAlive != true)
                    continue;

                if (p.Profile?.Info?.GroupId == groupId && Vector3.Distance(p.Position, position) <= radius)
                    return true;
            }

            return false;
        }

        public static void RefreshLootRegistry()
        {
            Logger.LogInfo("[GameWorldHandler] 🔄 Refreshing loot registries...");
            LootRegistry.Clear();
            LootBootstrapper.RegisterAllLoot();
            BotDeadBodyScanner.ScanAll();
        }

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

        /// <summary>
        ///     Attempts to locate the IZones instance in the current world. Used for zone name tagging.
        /// </summary>
        /// <summary>
        ///     Attempts to locate any active component that implements IZones.
        ///     Safe for multiplayer, headless, and no world scenarios.
        /// </summary>
        public static bool TryGetIZones(out IZones? zones)
        {
            zones = null;

            var world = CachedWorld;
            if (world == null)
                return false;

            var allBehaviours = object.FindObjectsOfType<MonoBehaviour>();
            for (var i = 0; i < allBehaviours.Length; i++)
            {
                var candidate = allBehaviours[i];
                if (candidate is IZones found)
                {
                    zones = found;
                    return true;
                }
            }

            return false;
        }

        public static void TryInitializeWorld()
        {
            if (IsInitialized)
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
            IsInitialized = true;
        }

        public static void UnhookBotSpawns()
        {
            if (_bootstrapHost != null)
            {
                object.Destroy(_bootstrapHost);
                _bootstrapHost = null;
            }

            HotspotRegistry.Clear();
            LootRegistry.Clear();
            IsInitialized = false;
            _hasWarnedNoWorld = false;

            Logger.LogInfo("[GameWorldHandler] 🔻 GameWorldHandler shut down.");
        }
    }
}