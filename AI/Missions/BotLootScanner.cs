#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Core;
using EFT;
using EFT.Interactive;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Looting
{
    /// <summary>
    /// Scans nearby environment for containers and lootable items.
    /// Filters by visibility, angle, and cooldown to avoid redundant looting.
    /// </summary>
    public class BotLootScanner
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _nextScanTime;
        private readonly Dictionary<string, float> _lootCooldowns = new(64);

        private const float ScanRadius = 12f;
        private const float MaxAngle = 120f;
        private const float HighestValueScanRadius = 20f;
        private const float ScanInterval = 1.5f;
        private const float LootCooldown = 6f;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Runtime Tick

        public void Tick(float deltaTime)
        {
            if (!CanEvaluate() || Time.time < _nextScanTime)
                return;

            _nextScanTime = Time.time + ScanInterval;

            if (CanLoot())
                TryLootNearby();
        }

        public void TryLootNearby()
        {
            if (!CanLoot())
                return;

            if (TryFindNearbyContainer(out var container) && container != null)
            {
                TryLootContainer(container);
                return;
            }

            if (TryFindNearbyItem(out var item) && item != null)
            {
                TryLootItem(item);
            }
        }

        #endregion

        #region Scanning Logic

        private bool TryFindNearbyContainer(out LootableContainer? result)
        {
            result = null;
            if (_bot == null) return false;

            float bestScore = float.MinValue;
            LootableContainer? best = null;

            foreach (var container in LootRegistry.Containers)
            {
                if (container == null || !container.enabled || IsOnCooldown(container.name))
                    continue;

                float dist = Vector3.Distance(_bot.Position, container.transform.position);
                if (dist > ScanRadius || !CanSeeTarget(container.transform.position))
                    continue;

                float score = 1f / dist;
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
            if (_bot == null) return false;

            float bestScore = float.MinValue;
            LootItem? best = null;

            foreach (var item in LootRegistry.Items)
            {
                if (item == null || !item.enabled || IsOnCooldown(item.name))
                    continue;

                float dist = Vector3.Distance(_bot.Position, item.transform.position);
                if (dist > ScanRadius || !CanSeeTarget(item.transform.position))
                    continue;

                float score = 1f / dist;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = item;
                }
            }

            result = best;
            return best != null;
        }

        #endregion

        #region Loot Execution

        private void TryLootContainer(LootableContainer container)
        {
            if (_bot == null || _bot.GetPlayer == null || !container.enabled)
                return;

            MarkCooldown(container.name);
            container.Interact(new InteractionResult(EInteractionType.Open));
        }

        private void TryLootItem(LootItem item)
        {
            if (_bot == null || _bot.GetPlayer == null || item.Item == null || !item.enabled)
                return;

            MarkCooldown(item.name);

            var inv = _bot.GetPlayer.InventoryController;
            var slot = inv.FindSlotToPickUp(item.Item);

            if (slot != null)
            {
                var move = InteractionsHandlerClass.Move(item.Item, slot, inv, true);
                if (move.Succeeded)
                    inv.TryRunNetworkTransaction(move, null);
            }
        }

        #endregion

        #region Value & Visibility

        public Vector3 GetHighestValueLootPoint()
        {
            if (_bot == null || FikaHeadlessDetector.IsHeadless)
                return Vector3.zero;

            float bestValue = 0f;
            Vector3 bestPoint = _bot.Position;

            foreach (var container in LootRegistry.Containers)
            {
                if (container == null || !container.enabled)
                    continue;

                float dist = Vector3.Distance(_bot.Position, container.transform.position);
                if (dist > HighestValueScanRadius || !CanSeeTarget(container.transform.position))
                    continue;

                float value = EstimateContainerValue(container);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestPoint = container.transform.position;
                }
            }

            return bestPoint;
        }

        private float EstimateContainerValue(LootableContainer container)
        {
            if (container.ItemOwner?.RootItem == null)
                return 0f;

            float total = 0f;
            foreach (var item in container.ItemOwner.RootItem.GetAllItems())
            {
                if (item?.Template?.CreditsPrice > 0)
                    total += item.Template.CreditsPrice;
            }

            return total;
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

            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, direction.magnitude + 0.3f, LayerMaskClass.HighPolyWithTerrainMaskAI))
            {
                return Vector3.Distance(hit.point, position) < 0.4f;
            }

            return true;
        }

        #endregion

        #region Cooldown Management

        private void MarkCooldown(string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
                _lootCooldowns[id] = Time.time + LootCooldown;
        }

        private bool IsOnCooldown(string id)
        {
            return !string.IsNullOrWhiteSpace(id) &&
                   _lootCooldowns.TryGetValue(id, out var expiresAt) &&
                   Time.time < expiresAt;
        }

        #endregion

        #region Evaluation Conditions

        private bool CanEvaluate()
        {
            return _bot != null && !_bot.IsDead && !FikaHeadlessDetector.IsHeadless;
        }

        private bool CanLoot()
        {
            if (_bot == null || _bot.IsDead)
                return false;

            if (_cache?.PanicHandler?.IsPanicking == true)
                return false;

            if (_bot.Memory?.GoalEnemy != null || (_bot.EnemiesController?.EnemyInfos.Count ?? 0) > 0)
                return false;

            return true;
        }

        #endregion
    }
}
