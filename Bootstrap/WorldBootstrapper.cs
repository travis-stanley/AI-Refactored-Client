#nullable enable

namespace AIRefactored.Bootstrap
{
    using System.Collections;

    using AIRefactored.AI.Hotspots;
    using AIRefactored.AI.Looting;
    using AIRefactored.AI.Navigation;
    using AIRefactored.AI.Optimization;
    using AIRefactored.AI.Threads;
    using AIRefactored.Core;
    using AIRefactored.Runtime;

    using BepInEx.Logging;

    using Comfort.Common;

    using EFT;

    using Unity.AI.Navigation;

    using UnityEngine;

    /// <summary>
    ///     Initializes AIRefactored world systems on scene load. Handles bot registration, NavMesh warm-up,
    ///     and AI brain injection in both multiplayer and headless environments.
    /// </summary>
    public sealed class WorldBootstrapper : MonoBehaviour
    {
        #region Static Manual Bootstrap

        /// <summary>
        ///     Triggers world system initialization for maps, safe for headless hosts and early plugin boot.
        /// </summary>
        public static void TryInitialize()
        {
            if (_hasInitialized)
                return;

            var bootstrapperObj = new GameObject("WorldBootstrapper (Injected)");
            DontDestroyOnLoad(bootstrapperObj);
            bootstrapperObj.AddComponent<WorldBootstrapper>();

            _hasInitialized = true;
            Logger.LogInfo("[WorldBootstrapper] ✅ Manual bootstrap injection complete.");
        }

        #endregion

        #region Fields

        private const float SweepInterval = 20f;

        private float _lastSweepTime = -999f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        private static bool _hasInitialized;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Logger.LogInfo("[WorldBootstrapper] 🟢 Awake() triggered.");

            this.StartCoroutine(this.WatchForWorld());
            BotWorkScheduler.AutoInjectFlushHost();

            if (FikaHeadlessDetector.IsHeadless)
                Logger.LogInfo("[WorldBootstrapper] 🧠 Headless mode detected. UI-dependent systems will be skipped.");
        }

        private void Update()
        {
            if (Time.time - this._lastSweepTime >= SweepInterval)
            {
                GameWorldHandler.EnforceBotBrains();
                this._lastSweepTime = Time.time;
            }

            GameWorldHandler.CleanupDeadBotsSmoothly();
        }

        private void OnDestroy()
        {
            if (Singleton<BotSpawner>.Instantiated)
                Singleton<BotSpawner>.Instance.OnBotCreated -= this.HandleBotCreated;

            Logger.LogInfo("[WorldBootstrapper] 🔻 Unloaded — cleanup complete.");

            HotspotRegistry.Clear();
            LootRegistry.Clear();
            NavPointRegistry.Clear();
        }

        #endregion

        #region World Initialization

        private IEnumerator WatchForWorld()
        {
            if (FikaHeadlessDetector.IsHeadless)
            {
                Logger.LogInfo("[WorldBootstrapper] 🧠 Skipping GameWorld wait — running in headless mode.");
                this.InitializeWorldSystems();
                yield break;
            }

            while (!SingletonExists())
                yield return null;

            if (Singleton<BotSpawner>.Instantiated)
                Singleton<BotSpawner>.Instance.OnBotCreated += this.HandleBotCreated;

            Logger.LogInfo("[WorldBootstrapper] 🌍 World detected — beginning AIRefactored initialization...");

            this.InitializeWorldSystems();

            yield return new WaitForSeconds(3f);
            GameWorldHandler.EnforceBotBrains();
        }

        private static bool SingletonExists()
        {
            return Singleton<BotSpawner>.Instantiated && Singleton<GameWorld>.Instantiated
                                                      && Singleton<GameWorld>.Instance != null;
        }

        private void InitializeWorldSystems()
        {
            var mapId = GameWorldHandler.GetCurrentMapName();

            Logger.LogInfo($"[WorldBootstrapper] 🔧 Initializing world systems for map: {mapId}");

            // Hotspots
            HotspotRegistry.Clear();
            HotspotRegistry.Initialize(mapId);
            Logger.LogInfo("[WorldBootstrapper] ✅ Hotspot registry initialized.");

            // Zone system
            ZoneAssignmentHelper.Clear();
            if (GameWorldHandler.TryGetIZones(out var zones))
            {
                ZoneAssignmentHelper.Initialize(zones);
                NavPointRegistry.InitializeZoneSystem(zones);
                Logger.LogInfo("[WorldBootstrapper] ✅ Zone assignment system initialized.");
            }
            else
            {
                Logger.LogWarning("[WorldBootstrapper] ⚠️ No IZones found — zone tagging disabled for this session.");
            }

            // Navpoints
            NavPointRegistry.Clear();
            NavPointRegistry.EnableSpatialIndexing(true);
            NavPointBootstrapper.RegisterAll(mapId);
            Logger.LogInfo("[WorldBootstrapper] ✅ NavPoint registry populated.");

            // Looting
            LootRegistry.Clear();
            LootBootstrapper.RegisterAllLoot();
            BotDeadBodyScanner.ScanAll();
            Logger.LogInfo("[WorldBootstrapper] ✅ Loot and dead body systems initialized.");

            // NavMesh retreat logic
            this.PrewarmAllNavMeshes();

            Logger.LogInfo("[WorldBootstrapper] ✅ AIRefactored world systems initialized.");
        }

        private void PrewarmAllNavMeshes()
        {
            var surfaces = FindObjectsOfType<NavMeshSurface>();
            var map = GameWorldHandler.GetCurrentMapName();

            for (var i = 0; i < surfaces.Length; i++)
            {
                var surface = surfaces[i];
                if (surface == null || !surface.enabled || !surface.gameObject.activeInHierarchy)
                    continue;

                surface.BuildNavMesh();
                BotCoverRetreatPlanner.RegisterSurface(map, surface);

                Logger.LogInfo($"[WorldBootstrapper] 🔄 Prewarmed NavMesh: {surface.name}");
            }
        }

        #endregion

        #region Bot Injection

        private void HandleBotCreated(BotOwner bot)
        {
            if (!IsEligibleBot(bot))
                return;

            var player = bot.GetPlayer;
            if (player?.gameObject == null)
                return;

            Logger.LogInfo($"[WorldBootstrapper] 🧠 Bot created: {player.Profile?.Info?.Nickname ?? "Unnamed"}");

            BotBrainGuardian.Enforce(player.gameObject);

            BotWorkScheduler.EnqueueToMainThread(() => { GameWorldHandler.TryAttachBotBrain(bot); });
        }

        private static bool IsEligibleBot(BotOwner? bot)
        {
            var player = bot?.GetPlayer;
            return bot != null && player != null && player.IsAI;
        }

        #endregion
    }
}