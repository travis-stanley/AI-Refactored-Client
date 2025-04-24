#nullable enable

using AIRefactored.AI.Optimization;
using AIRefactored.Bootstrap;
using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored
{
    [BepInPlugin("com.spock.airefactored", "AI-Refactored", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static ManualLogSource? _log;

        private void Awake()
        {
            _log = Logger;
            _log.LogWarning("[AIRefactored] 🔧 Plugin starting...");

            // Register logging and setup controller
            AIRefactoredController.Initialize(_log);
            WorldBootstrapper.TryInitialize();

            // Run FIKA-safe flush hook
            BotWorkScheduler.AutoInjectFlushHost();

            // If not headless, wait for GameWorld (client)
            if (!FikaHeadlessDetector.IsHeadless)
            {
                StartCoroutine(WaitForWorldBootstrap());
            }
            else
            {
                _log.LogWarning("[AIRefactored] 🧠 Headless mode detected — skipping GameWorld checks.");
                GameWorldHandler.HookBotSpawns(); // Manually trigger bootstrap
            }

            _log.LogWarning("[AIRefactored] ✅ Plugin.cs startup complete.");
        }

        /// <summary>
        /// Coroutine retry loop for safe client multiplayer startup.
        /// </summary>
        private System.Collections.IEnumerator WaitForWorldBootstrap()
        {
            float timeout = Time.time + 60f;

            while (!Comfort.Common.Singleton<ClientGameWorld>.Instantiated && Time.time < timeout)
                yield return null;

            if (Comfort.Common.Singleton<ClientGameWorld>.Instantiated)
            {
                _log!.LogWarning("[AIRefactored] ✅ GameWorld detected — proceeding with AIRefactored initialization.");
                GameWorldHandler.TryInitializeWorld();
            }
            else
            {
                _log!.LogWarning("[AIRefactored] ⚠ Timed out waiting for GameWorld. Skipping world hook.");
            }
        }

        private void OnDestroy()
        {
            GameWorldHandler.UnhookBotSpawns();
            _log?.LogInfo("[AIRefactored] 🔻 Plugin shutdown complete.");
        }

        public static ManualLogSource LoggerInstance =>
            _log ?? throw new System.NullReferenceException("[AIRefactored] LoggerInstance accessed before Awake().");
    }
}
