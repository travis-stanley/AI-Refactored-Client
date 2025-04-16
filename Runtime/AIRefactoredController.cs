#nullable enable

using UnityEngine;
using BepInEx.Logging;
using AIRefactored.AI.Optimization;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Runtime controller that initializes and manages global AIRefactored systems.
    /// Ticks AI subsystems like BotAIManager each frame.
    /// </summary>
    public class AIRefactoredController : MonoBehaviour
    {
        private static AIRefactoredController? _instance;
        private static ManualLogSource? _logger;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;

            if (_instance != null)
                return;

            var obj = new GameObject("AIRefactoredController");
            _instance = obj.AddComponent<AIRefactoredController>();

            DontDestroyOnLoad(obj);
            BotAIManager.Initialize(logger);

#if UNITY_EDITOR
            _logger.LogInfo("[AIRefactored] Controller initialized.");
#endif
        }

        private void Update()
        {
            BotAIManager.TickAll();
        }
    }
}
