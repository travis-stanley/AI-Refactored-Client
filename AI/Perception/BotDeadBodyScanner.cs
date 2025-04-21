#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Looting
{
    /// <summary>
    /// Scans for dead bodies nearby and attempts to loot them if safe.
    /// Used by BotMissionSystem to simulate realistic scavenging behavior.
    /// </summary>
    public class BotDeadBodyScanner
    {
        #region Config

        private const float ScanRadius = 12f;
        private const float MaxLootAngle = 120f;
        private const float Cooldown = 10f;
        private const float LootMemoryDuration = 15f;

        #endregion

        #region State

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private float _nextScanTime;

        private static readonly HashSet<string> _recentLootedBodies = new();
        private static readonly Dictionary<string, float> _lootTimestamps = new();

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;
        private static readonly bool DebugEnabled = false;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes this scanner with bot dependencies.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
        }

        #endregion

        #region Runtime Tick

        /// <summary>
        /// Called each frame to evaluate and possibly loot a body.
        /// </summary>
        public void Tick(float time)
        {
            if (!CanEvaluate(time))
                return;

            if (TryFindDeadBody(out Player corpse))
            {
                if (DebugEnabled)
                    Logger.LogDebug($"[DeadBodyScanner] Looting body of {corpse.Profile?.Info?.Nickname}");

                TryLootCorpse(corpse);
                _nextScanTime = time + Cooldown;

                if (!string.IsNullOrEmpty(corpse.ProfileId))
                    RegisterLooted(corpse.ProfileId);
            }
        }

        /// <summary>
        /// Called manually (e.g. on combat over) to attempt looting.
        /// </summary>
        public void TryLootNearby()
        {
            if (FikaHeadlessDetector.IsHeadless || !CanLootNow())
                return;

            if (TryFindDeadBody(out Player corpse))
            {
                if (DebugEnabled)
                    Logger.LogDebug($"[DeadBodyScanner] (Manual) Looting body of {corpse.Profile?.Info?.Nickname}");

                TryLootCorpse(corpse);

                if (!string.IsNullOrEmpty(corpse.ProfileId))
                    RegisterLooted(corpse.ProfileId);
            }
        }

        #endregion

        #region Evaluation Logic

        private bool CanEvaluate(float now)
        {
            if (_bot == null || _bot.IsDead || _bot.GetPlayer?.IsYourPlayer == true)
                return false;

            if (_cache == null || _cache.PanicHandler?.IsPanicking == true || now < _nextScanTime)
                return false;

            return CanLootNow();
        }

        private bool CanLootNow()
        {
            if (_bot == null || _bot.IsDead || _bot.GetPlayer?.IsYourPlayer == true)
                return false;

            if (_cache?.PanicHandler?.IsPanicking == true)
                return false;

            if (_bot.Memory?.GoalEnemy != null || (_bot.EnemiesController?.EnemyInfos.Count ?? 0) > 0)
                return false;

            var profile = BotRegistry.Get(_bot.ProfileId);
            if (profile != null && profile.Caution > 0.7f)
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

                if (!string.IsNullOrEmpty(p.ProfileId) && WasRecentlyLooted(p.ProfileId))
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
                    if (hit.collider?.transform.root == p.Transform?.Original?.root)
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
            if (_bot == null || _bot.IsDead || deadPlayer == null)
                return;

            var sourceInv = deadPlayer.InventoryController;
            var targetInv = _bot.GetPlayer?.InventoryController;

            if (sourceInv == null || targetInv == null)
                return;

            var lootable = deadPlayer.GetComponent<LootableContainer>();
            if (lootable != null && lootable.enabled)
            {
                lootable.Interact(new InteractionResult(EInteractionType.Open));

                if (DebugEnabled)
                    Logger.LogDebug($"[DeadBodyScanner] Interacted with LootableContainer on {deadPlayer.Profile?.Info?.Nickname}");

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

            for (int i = 0; i < slots.Length; i++)
            {
                var item = sourceInv.Inventory.Equipment.GetSlot(slots[i]).ContainedItem;
                if (item == null)
                    continue;

                var dest = targetInv.FindSlotToPickUp(item);
                if (dest == null)
                    continue;

                var move = InteractionsHandlerClass.Move(item, dest, targetInv, true);
                if (move.Succeeded)
                {
                    if (DebugEnabled)
                        Logger.LogDebug($"[DeadBodyScanner] Picked up item {item.Name.Localized()} from {slots[i]}");

                    targetInv.TryRunNetworkTransaction(move, null);
                    return;
                }
            }

            if (DebugEnabled)
                Logger.LogDebug($"[DeadBodyScanner] No items successfully looted from {deadPlayer.Profile?.Info?.Nickname}");
        }

        #endregion

        #region Memory Logic

        private static void RegisterLooted(string profileId)
        {
            _recentLootedBodies.Add(profileId);
            _lootTimestamps[profileId] = Time.time;
        }

        private static bool WasRecentlyLooted(string profileId)
        {
            if (_lootTimestamps.TryGetValue(profileId, out float time))
                return (Time.time - time) < LootMemoryDuration;

            return false;
        }

        #endregion
    }
}
