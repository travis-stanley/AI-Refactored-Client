#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Core;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace AIRefactored.AI.Looting
{
    /// <summary>
    /// Scans for lootable containers and items near the bot, prioritizing line-of-sight and proximity.
    /// Supports GetHighestValueLootPoint for mission targeting and TryLootNearby for reactive looting.
    /// </summary>
    public class BotLootScanner
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private const float ScanRadius = 12f;
        private const float MaxAngle = 120f;

        private float _nextScanTime = 0f;
        private const float ScanInterval = 1.5f;

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        public void Tick(float deltaTime)
        {
            if (_bot == null || FikaHeadlessDetector.IsHeadless || _bot.IsDead)
                return;

            float now = Time.time;
            if (now >= _nextScanTime)
            {
                _nextScanTime = now + ScanInterval;

                if (CanLoot())
                {
                    if (TryFindNearbyContainer(out var container))
                    {
                        Debug.DrawLine(_bot.Position, container.transform.position, Color.green, 1f);
                    }

                    if (TryFindNearbyItem(out var item))
                    {
                        Debug.DrawLine(_bot.Position, item.transform.position, Color.cyan, 1f);
                    }
                }
            }
        }

        public void TryLootNearby()
        {
            if (FikaHeadlessDetector.IsHeadless || !CanLoot())
                return;

            if (TryFindNearbyContainer(out var container))
            {
                TryLootContainer(container);
                return;
            }

            if (TryFindNearbyItem(out var item))
            {
                TryLootItem(item);
                return;
            }
        }

        public Vector3 GetHighestValueLootPoint()
        {
            if (FikaHeadlessDetector.IsHeadless || _bot == null)
                return _bot?.Position ?? Vector3.zero;

            float highestValue = 0f;
            Vector3 best = _bot.Position;

            var containers = GameObject.FindObjectsOfType<LootableContainer>();
            foreach (var container in containers)
            {
                if (container == null || !container.enabled)
                    continue;

                float value = EstimateContainerValue(container);
                if (value > highestValue)
                {
                    highestValue = value;
                    best = container.transform.position;
                }
            }

            return best;
        }

        private float EstimateContainerValue(LootableContainer container)
        {
            if (container.ItemOwner?.RootItem == null)
                return 0f;

            float total = 0f;
            foreach (var item in container.ItemOwner.RootItem.GetAllItems())
            {
                if (item?.Template != null && item.Template.CreditsPrice > 0)
                    total += item.Template.CreditsPrice;
            }

            return total;
        }

        private bool TryFindNearbyContainer(out LootableContainer container)
        {
            container = null!;
            if (_bot == null)
                return false;

            Collider[] hits = Physics.OverlapSphere(_bot.Position, ScanRadius, LayerMaskClass.LootLayerMask);

            foreach (var hit in hits)
            {
                var c = hit.GetComponent<LootableContainer>();
                if (c != null && c.enabled && CanSeeTarget(c.transform.position))
                {
                    container = c;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindNearbyItem(out LootItem item)
        {
            item = null!;
            if (_bot == null)
                return false;

            Collider[] hits = Physics.OverlapSphere(_bot.Position, ScanRadius, LayerMaskClass.LootLayerMask);

            foreach (var hit in hits)
            {
                var i = hit.GetComponent<LootItem>();
                if (i != null && i.enabled && CanSeeTarget(i.transform.position))
                {
                    item = i;
                    return true;
                }
            }

            return false;
        }

        private void TryLootContainer(LootableContainer container)
        {
            if (_bot?.GetPlayer == null || !container.enabled)
                return;

            container.Interact(new InteractionResult(EInteractionType.Open));
        }

        private void TryLootItem(LootItem item)
        {
            if (_bot?.GetPlayer == null || item.Item == null || !item.enabled)
                return;

            var inv = _bot.GetPlayer.InventoryController;
            var slot = inv.FindSlotToPickUp(item.Item);

            if (slot != null)
            {
                var move = InteractionsHandlerClass.Move(item.Item, slot, inv, true);
                if (move.Succeeded)
                    inv.TryRunNetworkTransaction(move, null);
            }
        }

        private bool CanLoot()
        {
            if (_bot == null || _bot.IsDead || _bot.GetPlayer?.IsYourPlayer == true)
                return false;

            if (_cache?.PanicHandler?.IsPanicking == true)
                return false;

            if (_bot.Memory?.GoalEnemy != null)
                return false;

            if (_bot.EnemiesController?.EnemyInfos.Count > 0)
                return false;

            return true;
        }

        private bool CanSeeTarget(Vector3 position)
        {
            if (_bot == null)
                return false;

            Vector3 origin = _bot.WeaponRoot.position;
            Vector3 direction = position - origin;
            float angle = Vector3.Angle(_bot.WeaponRoot.forward, direction);
            if (angle > MaxAngle)
                return false;

            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude + 0.5f, LayerMaskClass.HighPolyWithTerrainMaskAI))
                return hit.collider != null && hit.point == position;

            return true;
        }
    }
}
