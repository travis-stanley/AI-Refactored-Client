#nullable enable

using BepInEx.Logging;
using UnityEngine;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Global runtime controller for AI-Refactored. Initializes subsystems and holds persistent references.
    /// </summary>
    public class AIRefactoredController : MonoBehaviour
    {
        #region Static Fields

        private static AIRefactoredController? _instance;
        private static ManualLogSource? _logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the global AIRefactoredController if not already active.
        /// Persists across scene loads and provides shared access to global systems.
        /// </summary>
        /// <param name="logger">BepInEx logger to register for centralized output.</param>
        public static void Initialize(ManualLogSource logger)
        {
            if (_instance != null)
                return;

            _logger = logger;

            var controllerObject = new GameObject("AIRefactoredController");
            _instance = controllerObject.AddComponent<AIRefactoredController>();
            DontDestroyOnLoad(controllerObject);

            _logger.LogInfo("[AIRefactored] 🌐 Global controller initialized.");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Returns true if the AIRefactoredController has been initialized.
        /// </summary>
        public static bool IsInitialized => _instance != null;

        /// <summary>
        /// Provides a safe reference to the logger instance.
        /// </summary>
        public static ManualLogSource Logger =>
            _logger ?? throw new System.NullReferenceException("[AIRefactored] Logger accessed before initialization.");

        #endregion
    }
}
