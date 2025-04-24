#nullable enable

using AIRefactored.Runtime;
using EFT.Interactive;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Looting
{
    /// <summary>
    /// Centralized registry for all lootables in the scene.
    /// Bots query this instead of using expensive GetComponent calls.
    /// Injects runtime watchers where needed for dynamic state tracking.
    /// </summary>
    public static class LootRegistry
    {
        #region Fields

        private static readonly List<LootableContainer> _containers = new List<LootableContainer>(128);
        private static readonly List<LootItem> _items = new List<LootItem>(256);
        private static readonly HashSet<GameObject> _watchedObjects = new HashSet<GameObject>(256);

        #endregion

        #region Properties

        /// <summary> All registered lootable containers. </summary>
        public static IReadOnlyList<LootableContainer> Containers => _containers;

        /// <summary> All registered loose loot items. </summary>
        public static IReadOnlyList<LootItem> Items => _items;

        #endregion

        #region Public API

        /// <summary>
        /// Registers a lootable container into the global registry.
        /// </summary>
        /// <param name="container">The lootable container to register.</param>
        public static void RegisterContainer(LootableContainer? container)
        {
            if (container == null || _containers.Contains(container))
                return;

            _containers.Add(container);
            InjectWatcherIfNeeded(container.gameObject);
        }

        /// <summary>
        /// Registers a loot item into the global registry.
        /// </summary>
        /// <param name="item">The loose loot item to register.</param>
        public static void RegisterItem(LootItem? item)
        {
            if (item == null || _items.Contains(item))
                return;

            _items.Add(item);
            InjectWatcherIfNeeded(item.gameObject);
        }

        /// <summary>
        /// Clears all registered containers, items, and watched objects.
        /// Call on scene unload or mission reset.
        /// </summary>
        public static void Clear()
        {
            _containers.Clear();
            _items.Clear();
            _watchedObjects.Clear();
        }

        #endregion

        #region Internal Helpers

        private static void InjectWatcherIfNeeded(GameObject? go)
        {
            if (go == null || _watchedObjects.Contains(go))
                return;

            go.AddComponent<LootRuntimeWatcher>();
            _watchedObjects.Add(go);
        }

        #endregion
    }
}
