#nullable enable

namespace AIRefactored
{
    using System;
    using System.Collections;

    using AIRefactored.AI.Optimization;
    using AIRefactored.Bootstrap;
    using AIRefactored.Core;
    using AIRefactored.Runtime;

    using BepInEx;
    using BepInEx.Logging;

    using Comfort.Common;

    using EFT;

    using UnityEngine;

    [BepInPlugin("com.spock.airefactored", "AI-Refactored", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private static ManualLogSource? _log;

        public static ManualLogSource LoggerInstance =>
            _log ?? throw new NullReferenceException("[AIRefactored] LoggerInstance accessed before Awake().");

        private void Awake()
        {
            _log = this.Logger;
            _log.LogWarning("[AIRefactored] 🔧 Plugin starting...");

            // Register logging and setup controller
            AIRefactoredController.Initialize(_log);
            WorldBootstrapper.TryInitialize();

            // Run FIKA-safe flush hook
            BotWorkScheduler.AutoInjectFlushHost();

            // If not headless, wait for GameWorld (client)
            if (!FikaHeadlessDetector.IsHeadless)
            {
                this.StartCoroutine(this.WaitForWorldBootstrap());
            }
            else
            {
                _log.LogWarning("[AIRefactored] 🧠 Headless mode detected — skipping GameWorld checks.");
                GameWorldHandler.HookBotSpawns(); // Manually trigger bootstrap
            }

            _log.LogWarning("[AIRefactored] ✅ Plugin.cs startup complete.");
        }

        private void OnDestroy()
        {
            GameWorldHandler.UnhookBotSpawns();
            _log?.LogInfo("[AIRefactored] 🔻 Plugin shutdown complete.");
        }

        /// <summary>
        ///     Coroutine retry loop for safe client multiplayer startup.
        /// </summary>
        private IEnumerator WaitForWorldBootstrap()
        {
            var timeout = Time.time + 60f;

            while (!Singleton<ClientGameWorld>.Instantiated && Time.time < timeout)
                yield return null;

            if (Singleton<ClientGameWorld>.Instantiated)
            {
                _log!.LogWarning("[AIRefactored] ✅ GameWorld detected — proceeding with AIRefactored initialization.");
                GameWorldHandler.TryInitializeWorld();
            }
            else
            {
                _log!.LogWarning("[AIRefactored] ⚠ Timed out waiting for GameWorld. Skipping world hook.");
            }
        }
    }
}