#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Core;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using UnityEngine;

namespace AIRefactored.AI.Looting
{
    /// <summary>
    /// Scans for dead bodies nearby and attempts to loot them if safe.
    /// Used by BotMissionSystem to simulate realistic scavenging behavior.
    /// </summary>
    public class BotDeadBodyScanner
    {
        private const float ScanRadius = 12f;
        private const float MaxLootAngle = 120f;
        private const float Cooldown = 10f;

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private float _nextScanTime;

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        public void Tick(float time)
        {
            if (_bot == null || _bot.IsDead || _bot.GetPlayer?.IsYourPlayer == true)
                return;

            if (FikaHeadlessDetector.IsHeadless)
                return;

            if (_cache?.PanicHandler?.IsPanicking == true || time < _nextScanTime)
                return;

            if (!CanLootNow())
                return;

            if (TryFindDeadBody(out var corpse))
            {
                TryLootCorpse(corpse);
                _nextScanTime = time + Cooldown;
            }
        }

        public void TryLootNearby()
        {
            if (FikaHeadlessDetector.IsHeadless || !CanLootNow())
                return;

            if (TryFindDeadBody(out var corpse))
            {
                TryLootCorpse(corpse);
            }
        }

        private bool CanLootNow()
        {
            if (_bot == null || _bot.IsDead || _bot.GetPlayer?.IsYourPlayer == true)
                return false;

            if (_cache?.PanicHandler?.IsPanicking == true)
                return false;

            if (_bot.Memory?.GoalEnemy != null || _bot.EnemiesController?.EnemyInfos.Count > 0)
                return false;

            return true;
        }

        private bool TryFindDeadBody(out Player corpse)
        {
            corpse = null!;
            if (_bot == null)
                return false;

            Vector3 origin = _bot.Position;

            foreach (var p in GameWorldHandler.GetAllAlivePlayers())
            {
                if (p == null || p == _bot.GetPlayer || p.HealthController?.IsAlive != false)
                    continue;

                float dist = Vector3.Distance(origin, p.Position);
                if (dist > ScanRadius)
                    continue;

                Vector3 dir = (p.Position - origin).normalized;
                float angle = Vector3.Angle(_bot.WeaponRoot.forward, dir);
                if (angle > MaxLootAngle)
                    continue;

                if (Physics.Raycast(origin, dir, out RaycastHit hit, dist + 0.3f, LayerMaskClass.HighPolyWithTerrainMaskAI))
                {
                    if (hit.collider?.transform.root == p.Transform.Original.root)
                    {
                        corpse = p;
                        return true;
                    }
                }
            }

            return false;
        }

        private void TryLootCorpse(Player deadPlayer)
        {
            if (deadPlayer.InventoryController == null || _bot?.GetPlayer?.InventoryController == null)
                return;

            var lootable = deadPlayer.GetComponent<LootableContainer>();
            if (lootable != null && lootable.enabled)
            {
                lootable.Interact(new InteractionResult(EInteractionType.Open));
                return;
            }

            var slots = new[]
            {
                EquipmentSlot.FirstPrimaryWeapon,
                EquipmentSlot.SecondPrimaryWeapon,
                EquipmentSlot.Holster,
                EquipmentSlot.Backpack,
                EquipmentSlot.TacticalVest,
                EquipmentSlot.Pockets
            };

            var inv = _bot.GetPlayer.InventoryController;

            foreach (var slot in slots)
            {
                var item = deadPlayer.InventoryController.Inventory.Equipment.GetSlot(slot).ContainedItem;
                if (item == null)
                    continue;

                var dest = inv.FindSlotToPickUp(item);
                if (dest == null)
                    continue;

                var move = InteractionsHandlerClass.Move(item, dest, inv, true);
                if (move.Succeeded)
                {
                    inv.TryRunNetworkTransaction(move, null);
                    return;
                }
            }
        }
    }
}
