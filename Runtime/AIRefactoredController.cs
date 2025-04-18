#nullable enable

using BepInEx.Logging;
using UnityEngine;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Global runtime controller for AIRefactored. Initializes subsystems and holds persistent references.
    /// </summary>
    public class AIRefactoredController : MonoBehaviour
    {
        private static AIRefactoredController? _instance;
        private static ManualLogSource? _logger;

        /// <summary>
        /// Initializes the global controller once and persists it across scenes.
        /// </summary>
        public static void Initialize(ManualLogSource logger)
        {
            if (_instance != null)
                return;

            _logger = logger;

            var obj = new GameObject("AIRefactoredController");
            _instance = obj.AddComponent<AIRefactoredController>();
            DontDestroyOnLoad(obj);

            _logger.LogInfo("[AIRefactored] ✅ Global controller initialized.");
        }
    }
}
