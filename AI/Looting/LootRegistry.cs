#nullable enable

namespace AIRefactored.AI.Looting
{
    using System;
    using System.Collections.Generic;

    using AIRefactored.Runtime;

    using EFT.Interactive;

    using UnityEngine;

    /// <summary>
    ///     Centralized registry for all lootables in the scene.
    ///     Bots query this instead of using expensive GetComponent calls.
    ///     Injects runtime watchers where needed for dynamic state tracking.
    /// </summary>
    public static class LootRegistry
    {
        private static readonly List<LootableContainer> _containerBuffer = new(32);

        private static readonly HashSet<LootableContainer> _containers = new(128);

        private static readonly List<LootItem> _itemBuffer = new(64);

        private static readonly HashSet<LootItem> _items = new(256);

        private static readonly HashSet<GameObject> _watchedObjects = new(256);

        public static IReadOnlyCollection<LootableContainer> Containers => _containers;

        public static IReadOnlyCollection<LootItem> Items => _items;

        public static void Clear()
        {
            _containers.Clear();
            _items.Clear();
            _watchedObjects.Clear();
            _containerBuffer.Clear();
            _itemBuffer.Clear();
        }

        public static List<LootableContainer> GetNearbyContainers(Vector3 origin, float radius)
        {
            _containerBuffer.Clear();
            var radiusSq = radius * radius;

            foreach (var c in _containers)
            {
                if (c == null) continue;

                var distSq = (c.transform.position - origin).sqrMagnitude;
                if (distSq <= radiusSq)
                    _containerBuffer.Add(c);
            }

            return new List<LootableContainer>(_containerBuffer);
        }

        public static List<LootItem> GetNearbyItems(Vector3 origin, float radius)
        {
            _itemBuffer.Clear();
            var radiusSq = radius * radius;

            foreach (var i in _items)
            {
                if (i == null) continue;

                var distSq = (i.transform.position - origin).sqrMagnitude;
                if (distSq <= radiusSq)
                    _itemBuffer.Add(i);
            }

            return new List<LootItem>(_itemBuffer);
        }

        public static void RegisterContainer(LootableContainer? container)
        {
            if (container == null)
                return;

            if (!_containers.Add(container))
                return;

            InjectWatcherIfNeeded(container.gameObject);
        }

        public static void RegisterItem(LootItem? item)
        {
            if (item == null)
                return;

            if (!_items.Add(item))
                return;

            InjectWatcherIfNeeded(item.gameObject);
        }

        public static bool TryGetContainerByName(string name, out LootableContainer? found)
        {
            foreach (var c in _containers)
                if (c != null && c.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    found = c;
                    return true;
                }

            found = null;
            return false;
        }

        public static bool TryGetItemByName(string name, out LootItem? found)
        {
            foreach (var item in _items)
                if (item != null && item.name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    found = item;
                    return true;
                }

            found = null;
            return false;
        }

        private static void InjectWatcherIfNeeded(GameObject? go)
        {
            if (go == null || _watchedObjects.Contains(go))
                return;

            go.AddComponent<LootRuntimeWatcher>();
            _watchedObjects.Add(go);
        }
    }
}