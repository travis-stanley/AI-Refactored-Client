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

        private void Awake()
        {
            _log = Logger;
            _log.LogInfo("[AIRefactored] 🔧 Initializing AI-Refactored mod...");

            if (FikaHeadlessDetector.IsHeadless)
                _log.LogInfo("[AIRefactored] 🧠 Running in FIKA Headless Host mode.");
            else
                _log.LogInfo("[AIRefactored] 🧠 Running in Client/Interactive mode.");

            // === Core Boot ===
            AIRefactoredController.Initialize(_log);

            // === Bot brain protection & attach hook ===
            GameWorldHandler.HookBotSpawns();

            // === Harmony Patches ===
            _harmony = new Harmony("com.spock.airefactored");
            _harmony.PatchAll();

            _log.LogInfo("[AIRefactored] ✅ Initialization complete. Systems are online.");
        }

        private void OnDestroy()
        {
            GameWorldHandler.UnhookBotSpawns();

            _harmony?.UnpatchSelf();
            _log?.LogInfo("[AIRefactored] 🔻 Harmony patches removed.");
            _log?.LogInfo("[AIRefactored] 🔻 Plugin shutdown complete.");
        }

        #endregion

        #region Public Access

        public static ManualLogSource LoggerInstance =>
            _log ?? throw new System.NullReferenceException("[AIRefactored] LoggerInstance was accessed before plugin Awake().");

        #endregion
    }
}
