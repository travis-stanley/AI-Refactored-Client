#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Core;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Looting
{
    /// <summary>
    /// Scans for nearby dead players and loots if safe and opportunistic.
    /// Tracks previously looted corpses to avoid redundancy.
    /// </summary>
    public sealed class BotDeadBodyScanner
    {
        #region Constants

        private const float ScanRadius = 12f;
        private const float MaxLootAngle = 120f;
        private const float LootCooldown = 10f;
        private const float LootMemoryDuration = 15f;
        private const float RaycastPadding = 0.3f;

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private float _nextScanTime;

        private static readonly HashSet<string> _recentlyLooted = new(32);
        private static readonly Dictionary<string, float> _lootTimestamps = new(32);

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Public API

        public void Tick(float time)
        {
            if (!CanEvaluate(time))
                return;

            _nextScanTime = time + LootCooldown;
            TryLootOnce();
        }

        public void TryLootNearby() => TryLootOnce();

        #endregion

        #region Loot Execution

        private void TryLootOnce()
        {
            var corpse = FindLootableCorpse();
            if (corpse == null)
                return;

            LootCorpse(corpse);
            RememberLooted(corpse.ProfileId);
        }

        private Player? FindLootableCorpse()
        {
            if (_bot == null)
                return null;

            Vector3 origin = _bot.Position;
            Vector3 forward = _bot.WeaponRoot.forward;
            var players = GameWorldHandler.GetAllAlivePlayers();

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!IsValidCorpse(player))
                    continue;

                Vector3 toCorpse = player.Position - origin;
                float distance = toCorpse.magnitude;
                if (distance > ScanRadius)
                    continue;

                if (Vector3.Angle(forward, toCorpse.normalized) > MaxLootAngle)
                    continue;

                if (!HasLineOfSight(origin, toCorpse, distance, player))
                    continue;

                return player;
            }

            return null;
        }

        private bool IsValidCorpse(Player p)
        {
            return p != null &&
                   p.HealthController?.IsAlive == false &&
                   p != _bot?.GetPlayer &&
                   !string.IsNullOrEmpty(p.ProfileId) &&
                   !WasLootedRecently(p.ProfileId);
        }

        private bool HasLineOfSight(Vector3 origin, Vector3 direction, float dist, Player corpse)
        {
            if (!Physics.Raycast(origin, direction.normalized, out var hit, dist + RaycastPadding, LayerMaskClass.HighPolyWithTerrainMaskAI))
                return false;

            return hit.collider?.transform.root == corpse.Transform?.Original?.root;
        }

        #endregion

        #region Corpse Looting Logic

        private void LootCorpse(Player corpse)
        {
            var source = corpse.InventoryController;
            var target = _bot?.GetPlayer?.InventoryController;

            if (source == null || target == null)
                return;

            if (DeadBodyContainerCache.Get(corpse.ProfileId) is { enabled: true } container)
            {
                container.Interact(new InteractionResult(EInteractionType.Open));
                return;
            }

            TryStealBestItem(source, target);
        }

        private void TryStealBestItem(InventoryController source, InventoryController target)
        {
            EquipmentSlot[] prioritySlots =
            {
                EquipmentSlot.FirstPrimaryWeapon,
                EquipmentSlot.SecondPrimaryWeapon,
                EquipmentSlot.Holster,
                EquipmentSlot.TacticalVest,
                EquipmentSlot.Backpack,
                EquipmentSlot.Pockets
            };

            for (int i = 0; i < prioritySlots.Length; i++)
            {
                var slot = prioritySlots[i];
                var item = source.Inventory.Equipment.GetSlot(slot).ContainedItem;
                if (item == null)
                    continue;

                var destination = target.FindSlotToPickUp(item);
                if (destination == null)
                    continue;

                var move = InteractionsHandlerClass.Move(item, destination, target, true);
                if (move.Succeeded)
                {
                    target.TryRunNetworkTransaction(move, null);
                    return;
                }
            }
        }

        #endregion

        #region Loot Memory

        private void RememberLooted(string profileId)
        {
            _recentlyLooted.Add(profileId);
            _lootTimestamps[profileId] = Time.time;
        }

        private bool WasLootedRecently(string profileId)
        {
            return _lootTimestamps.TryGetValue(profileId, out float lastTime) &&
                   (Time.time - lastTime) < LootMemoryDuration;
        }

        #endregion

        #region Evaluation Guards

        private bool CanEvaluate(float time)
        {
            return _bot != null &&
                   !_bot.IsDead &&
                   _bot.GetPlayer?.IsYourPlayer != true &&
                   (_cache?.PanicHandler?.IsPanicking != true) &&
                   time >= _nextScanTime;
        }

        #endregion

        #region Startup Scan

        public static void ScanAll()
        {
            int registered = 0;

            foreach (var container in LootRegistry.Containers)
            {
                var players = GameWorldHandler.GetAllAlivePlayers();
                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player == null || player.HealthController?.IsAlive != false || string.IsNullOrEmpty(player.ProfileId))
                        continue;

                    if (Vector3.Distance(player.Position, container.transform.position) < 0.75f)
                    {
                        DeadBodyContainerCache.Register(player, container);
                        registered++;
                        break;
                    }
                }
            }
        }

        #endregion
    }
}
