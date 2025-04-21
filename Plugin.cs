#nullable enable

using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace AIRefactored
{
    /// <summary>
    /// Entry point for the AI-Refactored mod. Registers BepInEx plugin metadata and bootstraps global systems.
    /// </summary>
    [BepInPlugin("com.spock.airefactored", "AI-Refactored", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        #region Fields

        private static ManualLogSource? _log;
        private Harmony? _harmony;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// Called by BepInEx on plugin load. Initializes mod systems and Harmony patches.
        /// </summary>
        private void Awake()
        {
            _log = Logger;
            _log.LogInfo("[AIRefactored] 🔧 Initializing AI-Refactored mod...");

            if (FikaHeadlessDetector.IsHeadless)
                _log.LogInfo("[AIRefactored] 🧠 Running in FIKA Headless Host mode.");
            else
                _log.LogInfo("[AIRefactored] 🧠 Running in Client/Interactive mode.");

            // === Core System Init ===
            AIRefactoredController.Initialize(_log);

            // === World & Bot Hooking ===
            GameWorldHandler.HookBotSpawns();

            // === Harmony Patch Application ===
            _harmony = new Harmony("com.spock.airefactored");
            _harmony.PatchAll();

            _log.LogInfo("[AIRefactored] ✅ Initialization complete. Systems are online.");
        }

        /// <summary>
        /// Called by Unity on shutdown. Cleans up patching and detaches runtime hooks.
        /// </summary>
        private void OnDestroy()
        {
            GameWorldHandler.UnhookBotSpawns();

            _harmony?.UnpatchSelf();
            _log?.LogInfo("[AIRefactored] 🔻 Harmony patches removed.");
            _log?.LogInfo("[AIRefactored] 🔻 Plugin shutdown complete.");
        }

        #endregion

        #region Public Access

        /// <summary>
        /// Provides safe access to the logger once initialized.
        /// </summary>
        public static ManualLogSource LoggerInstance =>
            _log ?? throw new System.NullReferenceException("[AIRefactored] LoggerInstance was accessed before plugin Awake().");

        #endregion
    }
}
