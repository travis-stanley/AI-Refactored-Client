#nullable enable

using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Optimization;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Global runtime AI tick controller. Handles per-frame updates for all bot controllers.
    /// </summary>
    public static class BotAIManager
    {
        private static readonly List<BotComponentCache> _activeBots = new();
        private static readonly List<BotComponentCache> _pendingRemoval = new();
        private static ManualLogSource? _logger;

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
#if UNITY_EDITOR
            _logger.LogInfo("[AIRefactored-AI] BotAIManager initialized.");
#endif
        }

        /// <summary>
        /// Registers a bot's AI controller into the global manager.
        /// </summary>
        public static void Register(BotComponentCache cache)
        {
            if (!_activeBots.Contains(cache))
            {
                _activeBots.Add(cache);
#if UNITY_EDITOR
                _logger?.LogDebug($"[AIRefactored-AI] Registered bot: {cache.Bot?.Profile?.Info?.Nickname ?? "?"}");
#endif
            }
        }

        /// <summary>
        /// Marks a bot for safe removal from the tick system.
        /// </summary>
        public static void Unregister(BotComponentCache cache)
        {
            if (!_pendingRemoval.Contains(cache))
                _pendingRemoval.Add(cache);
        }

        /// <summary>
        /// Ticks all registered bots this frame. Call this from Plugin.Update().
        /// </summary>
        public static void TickAll()
        {
            float now = Time.time;

            // Cleanup
            for (int i = 0; i < _pendingRemoval.Count; i++)
            {
                _activeBots.Remove(_pendingRemoval[i]);
            }
            _pendingRemoval.Clear();

            // Tick AI
            for (int i = 0; i < _activeBots.Count; i++)
            {
                var cache = _activeBots[i];
                if (cache.TryGetComponent(out BotAIController? controller))
                {
                    controller.TickAI(now);
                }
            }
        }
    }
}
