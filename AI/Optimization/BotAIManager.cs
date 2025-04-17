#nullable enable

using System.Collections.Generic;
using BepInEx.Logging;
using UnityEngine;
using AIRefactored.AI.Core;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Global registry for active bot AIRefactored caches. No longer handles ticking — only tracks bots for diagnostics or utility purposes.
    /// </summary>
    public static class BotAIManager
    {
        #region Fields

        private static readonly List<BotComponentCache> _activeBots = new();
        private static readonly List<BotComponentCache> _pendingRemoval = new();
        private static ManualLogSource? _logger;

        #endregion

        #region Initialization

        public static void Initialize(ManualLogSource logger)
        {
            _logger = logger;
        }

        #endregion

        #region Registration

        /// <summary>
        /// Registers a bot cache to the global registry.
        /// </summary>
        public static void Register(BotComponentCache cache)
        {
            if (!_activeBots.Contains(cache))
                _activeBots.Add(cache);
        }

        /// <summary>
        /// Marks a bot cache for removal.
        /// </summary>
        public static void Unregister(BotComponentCache cache)
        {
            if (!_pendingRemoval.Contains(cache))
                _pendingRemoval.Add(cache);
        }

        #endregion

        #region Accessors

        /// <summary>
        /// Returns a read-only list of active bot caches.
        /// </summary>
        public static IReadOnlyList<BotComponentCache> GetAllBots()
        {
            ProcessRemovals();
            return _activeBots;
        }

        /// <summary>
        /// Cleans up pending removals.
        /// </summary>
        private static void ProcessRemovals()
        {
            if (_pendingRemoval.Count == 0)
                return;

            for (int i = 0; i < _pendingRemoval.Count; i++)
                _activeBots.Remove(_pendingRemoval[i]);

            _pendingRemoval.Clear();
        }

        #endregion
    }
}
