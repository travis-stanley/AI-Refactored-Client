#nullable enable

using UnityEngine;
using BepInEx.Logging;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Central controller for AIRefactored. Manages initialization and global AI subsystems.
    /// </summary>
    public class AIRefactoredController : MonoBehaviour
    {
        #region Fields

        private static AIRefactoredController? _instance;
        private static ManualLogSource? _logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the AIRefactored controller and global managers.
        /// </summary>
        /// <param name="logger">BepInEx logger instance for diagnostics.</param>
        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;

            if (_instance != null)
                return;

            var obj = new GameObject("AIRefactoredController");
            _instance = obj.AddComponent<AIRefactoredController>();
            DontDestroyOnLoad(obj);

            _logger.LogInfo("[AIRefactored] ✅ Global controller initialized.");
        }

        #endregion
    }
}
