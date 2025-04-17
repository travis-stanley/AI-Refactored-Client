#nullable enable

using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using AIRefactored.Runtime;

namespace AIRefactored
{
    [BepInPlugin("com.spock.airefactored", "AI-Refactored", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        private static ManualLogSource _logger = null!;

        private void Awake()
        {
            _logger = Logger;
            _logger.LogInfo("[AIRefactored] Initializing AIRefactored mod...");

            // === Core Runtime Setup ===
            AIRefactoredController.Initialize(_logger);

            // === Bot Lifecycle Bootstrapper ===
            var bootstrap = new GameObject("AIRefactored.BotInitializer");
            bootstrap.AddComponent<BotInitializer>();
            DontDestroyOnLoad(bootstrap);

            _logger.LogInfo("[AIRefactored] ✅ AIRefactored mod loaded and systems online.");
        }
    }
}
