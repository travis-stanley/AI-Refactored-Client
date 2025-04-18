#nullable enable

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
            _logger.LogInfo("[AIRefactored-Plugin] 🔧 Initializing AI-Refactored mod...");

            // === Core System Setup ===
            AIRefactoredController.Initialize(_logger);

            // === Bot Lifecycle Bootstrap ===
            var bootstrap = new GameObject("AIRefactored.BotInitializer");
            bootstrap.AddComponent<BotInitializer>();
            DontDestroyOnLoad(bootstrap);

            _logger.LogInfo("[AIRefactored-Plugin] ✅ AI-Refactored mod initialized and systems online.");
        }

        #endregion

        #region Public Access

        /// <summary>
        /// Provides global access to the plugin's logger instance.
        /// </summary>
        public static ManualLogSource LoggerInstance =>
            _logger ?? throw new System.NullReferenceException("LoggerInstance accessed before Awake() initialized it.");

        #endregion
    }
}
