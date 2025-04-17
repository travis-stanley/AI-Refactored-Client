#nullable enable

using UnityEngine;
using BepInEx.Logging;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Global runtime controller for AIRefactored. Manages one-time setup of subsystems
    /// and holds persistent references for mod lifecycle coordination.
    /// </summary>
    public class AIRefactoredController : MonoBehaviour
    {
        #region Fields

        private static AIRefactoredController? _instance;
        private static ManualLogSource? _logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the global controller and prevents destruction across scenes.
        /// Will not reinitialize if already present.
        /// </summary>
        /// <param name="logger">Logger instance used for mod diagnostics and status.</param>
        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;

            if (_instance != null)
                return;

            var obj = new GameObject("AIRefactoredController");
            _instance = obj.AddComponent<AIRefactoredController>();
            DontDestroyOnLoad(obj);

            _logger?.LogInfo("[AIRefactored] ✅ Global controller initialized.");
        }

        #endregion
    }
}
