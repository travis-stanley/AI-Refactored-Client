#nullable enable

namespace AIRefactored.AI.Looting
{
    using System.Collections.Generic;

    using AIRefactored.AI.Core;
    using AIRefactored.Core;

    using EFT;
    using EFT.Interactive;
    using EFT.InventoryLogic;

    using UnityEngine;

    /// <summary>
    ///     Scans for nearby dead players and loots if safe and opportunistic.
    ///     Tracks previously looted corpses to avoid redundancy.
    /// </summary>
    public sealed class BotDeadBodyScanner
    {
        private const float LootCooldown = 10f;

        private const float LootMemoryDuration = 15f;

        private const float MaxLootAngle = 120f;

        private const float RaycastPadding = 0.3f;

        private const float ScanRadius = 12f;

        private static readonly Dictionary<string, float> _lootTimestamps = new(32);

        private static readonly HashSet<string> _recentlyLooted = new(32);

        private BotOwner? _bot;

        private BotComponentCache? _cache;

        private float _nextScanTime;

        public static void ScanAll()
        {
            var registered = 0;

            foreach (var container in LootRegistry.Containers)
            {
                var players = GameWorldHandler.GetAllAlivePlayers();
                for (var i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player == null || player.HealthController?.IsAlive != false
                                       || string.IsNullOrEmpty(player.ProfileId))
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

        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache;
            this._bot = cache.Bot;
        }

        public void Tick(float time)
        {
            if (!this.CanEvaluate(time))
                return;

            this._nextScanTime = time + LootCooldown;
            this.TryLootOnce();
        }

        public void TryLootNearby()
        {
            this.TryLootOnce();
        }

        private bool CanEvaluate(float time)
        {
            return this._bot != null && !this._bot.IsDead && this._bot.GetPlayer?.IsYourPlayer != true
                   && this._cache?.PanicHandler?.IsPanicking != true && time >= this._nextScanTime;
        }

        private Player? FindLootableCorpse()
        {
            if (this._bot == null)
                return null;

            var origin = this._bot.Position;
            var forward = this._bot.WeaponRoot.forward;
            var players = GameWorldHandler.GetAllAlivePlayers();

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (!this.IsValidCorpse(player))
                    continue;

                var toCorpse = player.Position - origin;
                var distance = toCorpse.magnitude;
                if (distance > ScanRadius)
                    continue;

                if (Vector3.Angle(forward, toCorpse.normalized) > MaxLootAngle)
                    continue;

                if (!this.HasLineOfSight(origin, toCorpse, distance, player))
                    continue;

                return player;
            }

            return null;
        }

        private bool HasLineOfSight(Vector3 origin, Vector3 direction, float dist, Player corpse)
        {
            if (!Physics.Raycast(
                    origin,
                    direction.normalized,
                    out var hit,
                    dist + RaycastPadding,
                    LayerMaskClass.HighPolyWithTerrainMaskAI))
                return false;

            return hit.collider?.transform.root == corpse.Transform?.Original?.root;
        }

        private bool IsValidCorpse(Player p)
        {
            return p != null && p.HealthController?.IsAlive == false && p != this._bot?.GetPlayer
                   && !string.IsNullOrEmpty(p.ProfileId) && !this.WasLootedRecently(p.ProfileId);
        }

        private void LootCorpse(Player corpse)
        {
            var source = corpse.InventoryController;
            var target = this._bot?.GetPlayer?.InventoryController;

            if (source == null || target == null)
                return;

            if (DeadBodyContainerCache.Get(corpse.ProfileId) is { enabled: true } container)
            {
                container.Interact(new InteractionResult(EInteractionType.Open));
                return;
            }

            this.TryStealBestItem(source, target);
        }

        private void RememberLooted(string profileId)
        {
            _recentlyLooted.Add(profileId);
            _lootTimestamps[profileId] = Time.time;
        }

        private void TryLootOnce()
        {
            var corpse = this.FindLootableCorpse();
            if (corpse == null)
                return;

            this.LootCorpse(corpse);
            this.RememberLooted(corpse.ProfileId);
        }

        private void TryStealBestItem(InventoryController source, InventoryController target)
        {
            EquipmentSlot[] prioritySlots =
                {
                    EquipmentSlot.FirstPrimaryWeapon, EquipmentSlot.SecondPrimaryWeapon, EquipmentSlot.Holster,
                    EquipmentSlot.TacticalVest, EquipmentSlot.Backpack, EquipmentSlot.Pockets
                };

            for (var i = 0; i < prioritySlots.Length; i++)
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
                    target.TryRunNetworkTransaction(move);
                    return;
                }
            }
        }

        private bool WasLootedRecently(string profileId)
        {
            return _lootTimestamps.TryGetValue(profileId, out var lastTime)
                   && Time.time - lastTime < LootMemoryDuration;
        }
    }
}