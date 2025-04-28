#nullable enable

namespace AIRefactored.AI.Looting
{
    using System.Collections.Generic;

    using AIRefactored.AI.Core;
    using AIRefactored.Core;

    using EFT;
    using EFT.Interactive;

    using UnityEngine;

    /// <summary>
    ///     Scans nearby environment for containers and lootable items.
    ///     Filters by visibility, angle, and cooldown to avoid redundant looting.
    /// </summary>
    public class BotLootScanner
    {
        private const float HighestValueScanRadius = 20f;

        private const float LootCooldown = 6f;

        private const float MaxAngle = 120f;

        private const float ScanInterval = 1.5f;

        private const float ScanRadius = 12f;

        private readonly Dictionary<string, float> _lootCooldowns = new(64);

        private BotOwner? _bot;

        private BotComponentCache? _cache;

        private float _nextScanTime;

        public Vector3 GetHighestValueLootPoint()
        {
            if (this._bot == null || FikaHeadlessDetector.IsHeadless)
                return Vector3.zero;

            var bestValue = 0f;
            var bestPoint = this._bot.Position;

            foreach (var container in LootRegistry.Containers)
            {
                if (container == null || !container.enabled)
                    continue;

                var dist = Vector3.Distance(this._bot.Position, container.transform.position);
                if (dist > HighestValueScanRadius || !this.CanSeeTarget(container.transform.position))
                    continue;

                var value = this.EstimateContainerValue(container);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestPoint = container.transform.position;
                }
            }

            return bestPoint;
        }

        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache;
            this._bot = cache.Bot;
        }

        public void Tick(float deltaTime)
        {
            if (!this.CanEvaluate() || Time.time < this._nextScanTime)
                return;

            this._nextScanTime = Time.time + ScanInterval;

            if (this.CanLoot()) this.TryLootNearby();
        }

        public void TryLootNearby()
        {
            if (!this.CanLoot())
                return;

            if (this.TryFindNearbyContainer(out var container) && container != null)
            {
                this.TryLootContainer(container);
                return;
            }

            if (this.TryFindNearbyItem(out var item) && item != null) this.TryLootItem(item);
        }

        private bool CanEvaluate()
        {
            return this._bot != null && !this._bot.IsDead && !FikaHeadlessDetector.IsHeadless;
        }

        private bool CanLoot()
        {
            if (this._bot == null || this._bot.IsDead)
                return false;

            if (this._cache?.PanicHandler?.IsPanicking == true)
                return false;

            if (this._bot.Memory?.GoalEnemy != null)
                return false;

            return this._bot.EnemiesController?.EnemyInfos.Count <= 0;
        }

        private bool CanSeeTarget(Vector3 position)
        {
            if (this._bot == null)
                return false;

            var origin = this._bot.WeaponRoot.position;
            var direction = position - origin;
            var angle = Vector3.Angle(this._bot.WeaponRoot.forward, direction);

            if (angle > MaxAngle)
                return false;

            if (Physics.Raycast(
                    origin,
                    direction.normalized,
                    out var hit,
                    direction.magnitude + 0.3f,
                    LayerMaskClass.HighPolyWithTerrainMaskAI)) return Vector3.Distance(hit.point, position) < 0.4f;

            return true;
        }

        private float EstimateContainerValue(LootableContainer container)
        {
            if (container.ItemOwner?.RootItem == null)
                return 0f;

            var total = 0f;
            foreach (var item in container.ItemOwner.RootItem.GetAllItems())
                if (item?.Template?.CreditsPrice > 0)
                    total += item.Template.CreditsPrice;

            return total;
        }

        private bool IsOnCooldown(string id)
        {
            return !string.IsNullOrWhiteSpace(id) && this._lootCooldowns.TryGetValue(id.Trim(), out var expiresAt)
                                                  && Time.time < expiresAt;
        }

        private void MarkCooldown(string id)
        {
            if (!string.IsNullOrWhiteSpace(id)) this._lootCooldowns[id.Trim()] = Time.time + LootCooldown;
        }

        private bool TryFindNearbyContainer(out LootableContainer? result)
        {
            result = null;
            if (this._bot == null) return false;

            var bestScore = float.MinValue;
            LootableContainer? best = null;

            foreach (var container in LootRegistry.Containers)
            {
                if (container == null || !container.enabled || this.IsOnCooldown(container.name))
                    continue;

                var dist = Vector3.Distance(this._bot.Position, container.transform.position);
                if (dist > ScanRadius || !this.CanSeeTarget(container.transform.position))
                    continue;

                var score = 1f / dist;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = container;
                }
            }

            result = best;
            return best != null;
        }

        private bool TryFindNearbyItem(out LootItem? result)
        {
            result = null;
            if (this._bot == null) return false;

            var bestScore = float.MinValue;
            LootItem? best = null;

            foreach (var item in LootRegistry.Items)
            {
                if (item == null || !item.enabled || this.IsOnCooldown(item.name))
                    continue;

                var dist = Vector3.Distance(this._bot.Position, item.transform.position);
                if (dist > ScanRadius || !this.CanSeeTarget(item.transform.position))
                    continue;

                var score = 1f / dist;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = item;
                }
            }

            result = best;
            return best != null;
        }

        private void TryLootContainer(LootableContainer container)
        {
            if (this._bot?.GetPlayer == null || !container.enabled)
                return;

            this.MarkCooldown(container.name);
            container.Interact(new InteractionResult(EInteractionType.Open));
        }

        private void TryLootItem(LootItem item)
        {
            if (this._bot?.GetPlayer == null || item.Item == null || !item.enabled)
                return;

            this.MarkCooldown(item.name);

            var inv = this._bot.GetPlayer.InventoryController;
            var slot = inv.FindSlotToPickUp(item.Item);

            if (slot != null)
            {
                var move = InteractionsHandlerClass.Move(item.Item, slot, inv, true);
                if (move.Succeeded)
                    inv.TryRunNetworkTransaction(move);
            }
        }
    }
}