#nullable enable

using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using AIRefactored.Runtime;

namespace AIRefactored
{
    /// <summary>
    /// Entry point for the AI-Refactored mod. Registers BepInEx plugin metadata and bootstraps global systems.
    /// </summary>
    [BepInPlugin("com.spock.airefactored", "AI-Refactored", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        #region Fields

        private static ManualLogSource _logger = null!;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Called automatically by BepInEx when the plugin is loaded.
        /// Initializes the global controller and bot AI lifecycle systems.
        /// </summary>
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

        #endregion
    }
}
