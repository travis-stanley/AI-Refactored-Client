#nullable enable

using AIRefactored.Core;
using BepInEx.Logging;
using System.Collections;
using UnityEngine;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Global runtime controller for AI-Refactored.
    /// Initializes subsystems and holds persistent references for global logic, logging, and lifecycle safety.
    /// </summary>
    public sealed class AIRefactoredController : MonoBehaviour
    {
        #region Static Fields

        private static AIRefactoredController? _instance;
        private static ManualLogSource? _logger;
        private bool _bootstrapped;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the global AIRefactored controller. Registers logging and ensures boot loop.
        /// </summary>
        public static void Initialize(ManualLogSource logger)
        {
            if (_instance != null)
            {
                logger.LogWarning("[AIRefactored] Attempted to reinitialize controller — already active.");
                return;
            }

            _logger = logger;

            GameObject host = new GameObject("AIRefactoredController");
            _instance = host.AddComponent<AIRefactoredController>();
            DontDestroyOnLoad(host);

            _logger.LogInfo("[AIRefactored] 🌐 Global controller initialized and awaiting world load.");
            _instance.StartCoroutine(_instance.LateBootCoroutine());
        }

        #endregion

        #region World Bootstrap Coroutine

        /// <summary>
        /// Periodically attempts to bootstrap world systems after scene load.
        /// Ensures reliable startup without race conditions.
        /// </summary>
        private IEnumerator LateBootCoroutine()
        {
            float waitUntil = Time.time + 60f;

            while (!_bootstrapped)
            {
                try
                {
                    GameWorldHandler.TryInitializeWorld();

                    if (GameWorldHandler.IsInitialized)
                    {
                        _bootstrapped = true;
                        _logger?.LogInfo("[AIRefactored] ✅ World systems bootstrapped.");
                        yield break;
                    }

                    if (FikaHeadlessDetector.IsHeadless && Time.time > waitUntil)
                    {
                        _logger?.LogWarning("[AIRefactored] ⚠ Headless fallback triggered. Proceeding with manual spawn hook.");
                        GameWorldHandler.HookBotSpawns();
                        _bootstrapped = true;
                        yield break;
                    }
                }
                catch (System.Exception ex)
                {
                    _logger?.LogError($"[AIRefactored] ❌ GameWorld bootstrap failed: {ex.Message}\n{ex.StackTrace}");
                }

                yield return new WaitForSeconds(0.5f);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// True if the global controller is initialized and active.
        /// </summary>
        public static bool IsInitialized => _instance != null;

        /// <summary>
        /// Accesses the registered logger. Throws if accessed before plugin boot.
        /// </summary>
        public static ManualLogSource Logger =>
            _logger ?? throw new System.InvalidOperationException("[AIRefactored] Logger accessed before initialization.");

        #endregion
    }
}
