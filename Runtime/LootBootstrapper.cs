#nullable enable

namespace AIRefactored.Runtime
{
    using System.Collections.Generic;

    using AIRefactored.AI.Looting;
    using AIRefactored.Core;

    using EFT;
    using EFT.Interactive;

    using UnityEngine;

    /// <summary>
    ///     Registers all lootable objects on the map, including containers and loose loot.
    ///     Associates loot containers with nearby corpses for AI logic.
    ///     Should be executed once per scene during world initialization.
    /// </summary>
    public static class LootBootstrapper
    {
        /// <summary>
        ///     Registers all lootable containers and loose items in the scene for AI interaction.
        /// </summary>
        public static void RegisterAllLoot()
        {
            if (!GameWorldHandler.IsInitialized)
                return;

            var containers = object.FindObjectsOfType<LootableContainer>();
            var items = object.FindObjectsOfType<LootItem>();
            var players = GameWorldHandler.GetAllAlivePlayers();

            var hasContainers = containers != null && containers.Length > 0;
            var hasItems = items != null && items.Length > 0;

            if (!hasContainers && !hasItems)
                return;

            if (hasContainers)
                RegisterContainers(containers!, players);

            if (hasItems)
                RegisterLooseItems(items!);
        }

        private static void RegisterContainers(LootableContainer[] containers, List<Player> players)
        {
            for (var i = 0; i < containers.Length; i++)
            {
                var container = containers[i];
                if (container == null || !container.enabled)
                    continue;

                LootRegistry.RegisterContainer(container);

                if (!FikaHeadlessDetector.IsHeadless)
                    TryRegisterCorpseContainer(container, players);
            }
        }

        private static void RegisterLooseItems(LootItem[] items)
        {
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null || !item.enabled)
                    continue;

                LootRegistry.RegisterItem(item);
            }
        }

        /// <summary>
        ///     Associates a loot container with a dead player if it's nearby or shares transform root.
        /// </summary>
        private static void TryRegisterCorpseContainer(LootableContainer container, List<Player> players)
        {
            if (players == null || players.Count == 0)
                return;

            var containerRoot = container.transform.root;

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.HealthController?.IsAlive != false)
                    continue;

                var playerRoot = player.Transform?.Original?.root;
                if (playerRoot == null || containerRoot == null)
                    continue;

                var isSameRoot = playerRoot == containerRoot;
                var isClose = Vector3.Distance(player.Position, container.transform.position) < 1.0f;

                if (isSameRoot || isClose)
                {
                    DeadBodyContainerCache.Register(player, container);
                    break;
                }
            }
        }
    }
}