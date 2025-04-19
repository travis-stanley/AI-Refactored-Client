#nullable enable

using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace AIRefactored
{
    /// <summary>
    /// Entry point for the AI-Refactored mod. Registers BepInEx plugin metadata and bootstraps global systems.
    /// </summary>
    [BepInPlugin("com.spock.airefactored", "AI-Refactored", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        #region Fields

        /// <summary>
        /// Static reference to the plugin's logger.
        /// </summary>
        private static ManualLogSource? _logger;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Called automatically by BepInEx when the mod is loaded.
        /// Initializes all core runtime systems for AI-Refactored.
        /// </summary>
        private void Awake()
        {
            _logger = Logger;
            _logger.LogInfo("[AIRefactored] 🔧 Initializing AI-Refactored mod...");

            if (FikaHeadlessDetector.IsHeadless)
                _logger.LogInfo("[AIRefactored] 🧠 Running in FIKA Headless Host mode.");
            else
                _logger.LogInfo("[AIRefactored] 🧠 Running in Client/Interactive mode.");

            // === Core Boot ===
            AIRefactoredController.Initialize(_logger);

            // === Bot Initialization ===
            var bootstrap = new GameObject("AIRefactored.BotInitializer");
            bootstrap.AddComponent<BotBrainBootstrapper>();
            DontDestroyOnLoad(bootstrap);

            _logger.LogInfo("[AIRefactored] ✅ Initialization complete. Systems are online.");
        }

        #endregion

        #region Public Access

        /// <summary>
        /// Provides global access to the plugin's logger instance.
        /// </summary>
        public static ManualLogSource LoggerInstance =>
            _logger ?? throw new System.NullReferenceException("[AIRefactored] LoggerInstance was accessed before plugin Awake().");

        #endregion
    }
}
