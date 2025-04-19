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
        #region Fields

        private static AIRefactoredController? _instance;
        private static ManualLogSource? _logger;

        #endregion

        #region Public API

        /// <summary>
        /// Initializes the global AIRefactoredController if not already active.
        /// Persists across scene loads.
        /// </summary>
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

        /// <summary>
        /// Returns true if the AIRefactoredController has been initialized.
        /// </summary>
        public static bool IsInitialized => _instance != null;

        /// <summary>
        /// Returns a reference to the logger, or throws if not yet initialized.
        /// </summary>
        public static ManualLogSource Logger =>
            _logger ?? throw new System.NullReferenceException("[AIRefactored] Logger accessed before initialization.");

        #endregion
    }
}
