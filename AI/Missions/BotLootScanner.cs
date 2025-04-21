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
    /// Supports <see cref="GetHighestValueLootPoint"/> for mission targeting and <see cref="TryLootNearby"/> for reactive looting.
    /// </summary>
    public class BotLootScanner
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private const float ScanRadius = 12f;
        private const float MaxAngle = 120f;
        private const float HighestValueScanRadius = 20f;
        private const float ScanInterval = 1.5f;

        private float _nextScanTime = 0f;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the scanner with bot context and cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Called each frame to trigger scan logic at fixed intervals.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!CanEvaluate() || Time.time < _nextScanTime)
                return;

            _nextScanTime = Time.time + ScanInterval;

            if (!CanLoot())
                return;

            if (TryFindNearbyContainer(out var container))
                Debug.DrawLine(_bot!.Position, container.transform.position, Color.green, 1f);

            if (TryFindNearbyItem(out var item))
                Debug.DrawLine(_bot!.Position, item.transform.position, Color.cyan, 1f);
        }

        /// <summary>
        /// Attempts to loot a nearby container or item.
        /// </summary>
        public void TryLootNearby()
        {
            if (!CanLoot())
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

        /// <summary>
        /// Returns the location of the highest-value nearby container.
        /// </summary>
        public Vector3 GetHighestValueLootPoint()
        {
            if (_bot == null || FikaHeadlessDetector.IsHeadless)
                return Vector3.zero;

            float highestValue = 0f;
            Vector3 best = _bot.Position;

            Collider[] hits = Physics.OverlapSphere(_bot.Position, HighestValueScanRadius, LayerMaskClass.LootLayerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var container = hits[i].GetComponent<LootableContainer>();
                if (container == null || !container.enabled || !CanSeeTarget(container.transform.position))
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

        #endregion

        #region Private Logic

        private bool CanEvaluate()
        {
            return _bot != null && !_bot.IsDead && !FikaHeadlessDetector.IsHeadless;
        }

        private bool CanLoot()
        {
            if (_bot == null || _bot.IsDead || _bot.GetPlayer?.IsYourPlayer == true)
                return false;

            if (_cache?.PanicHandler?.IsPanicking == true)
                return false;

            if (_bot.Memory?.GoalEnemy != null || (_bot.EnemiesController?.EnemyInfos.Count ?? 0) > 0)
                return false;

            return true;
        }

        private bool TryFindNearbyContainer(out LootableContainer container)
        {
            container = null!;
            if (_bot == null)
                return false;

            Collider[] hits = Physics.OverlapSphere(_bot.Position, ScanRadius, LayerMaskClass.LootLayerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var c = hits[i].GetComponent<LootableContainer>();
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
            for (int i = 0; i < hits.Length; i++)
            {
                var iLoot = hits[i].GetComponent<LootItem>();
                if (iLoot != null && iLoot.enabled && CanSeeTarget(iLoot.transform.position))
                {
                    item = iLoot;
                    return true;
                }
            }

            return false;
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
            {
                var root = hit.collider.GetComponentInParent<Transform>();
                return root != null && (root.position - position).sqrMagnitude < 0.5f;
            }

            return true;
        }

        #endregion
    }
}
