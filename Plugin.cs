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

        private static ManualLogSource? _logger;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Called when the AI-Refactored mod is loaded by BepInEx.
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
        /// Globally accessible logger for use by other components.
        /// </summary>
        public static ManualLogSource LoggerInstance =>
            _logger ?? throw new System.NullReferenceException("LoggerInstance accessed before Awake() initialized it.");

        #endregion
    }
}
