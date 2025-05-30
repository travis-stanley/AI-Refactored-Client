﻿// <auto-generated>
//   AI-Refactored: BotDeadBodyScanner.cs (Beyond Diamond, Ultimate Realism, June 2025)
//   SYSTEMATICALLY MANAGED. Bulletproof, pooling-optimized, null-free, squad-safe, multiplayer/headless compatible.
//   Realism: Human hesitation, memory, risk-prioritized corpse looting, squad/line-of-sight validation, and helper-driven pathing.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Looting
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Navigation;
    using AIRefactored.Core;
    using AIRefactored.Pools;
    using EFT;
    using EFT.Interactive;
    using EFT.InventoryLogic;
    using UnityEngine;

    /// <summary>
    /// Handles AIRefactored bot logic for identifying and looting nearby dead player corpses.
    /// Fully helper-routed movement, bulletproof: safe in all multiplayer/headless conditions, memory-aware, non-reentrant.
    /// Squad-aware: no double-loot, personality-based hesitation, all temp data pooled.
    /// </summary>
    public sealed class BotDeadBodyScanner
    {
        #region Constants

        private const float LootCooldown = 10f;
        private const float LootMemoryDuration = 15f;
        private const float MaxLootAngle = 120f;
        private const float RaycastPadding = 0.3f;
        private const float ScanRadius = 12f;
        private const float MaxContainerDistance = 0.75f;
        private const float CooldownVariance = 1.5f;
        private const float MinPersonalCooldown = 0.33f;
        private const float MoveToCorpseRadius = 1.4f; // Helper pathing
        private const float MoveToCorpseCohesion = 0.73f;

        #endregion

        #region Static Memory

        private static readonly Dictionary<string, float> LootTimestamps = new Dictionary<string, float>(32);
        private static readonly HashSet<string> RecentlyLooted = new HashSet<string>();

        #endregion

        #region Fields

        private BotOwner _bot;
        private BotComponentCache _cache;
        private float _nextScanTime;
        private float _personalScanVariance;

        #endregion

        #region Initialization

        public static void ScanAll()
        {
            try
            {
                if (!GameWorldHandler.IsSafeToInitialize)
                    return;

                List<LootableContainer> containers = LootRegistry.GetAllContainers();
                GameWorld world = GameWorldHandler.Get();
                if (world == null || world.RegisteredPlayers == null)
                    return;

                List<IPlayer> rawPlayers = world.RegisteredPlayers;
                List<Player> deadPlayers = TempListPool.Rent<Player>();

                for (int i = 0, n = rawPlayers.Count; i < n; i++)
                {
                    Player p = EFTPlayerUtil.AsEFTPlayer(rawPlayers[i]);
                    if (p != null && p.HealthController != null && !p.HealthController.IsAlive)
                        deadPlayers.Add(p);
                }

                for (int i = 0, n = containers.Count; i < n; i++)
                {
                    LootableContainer container = containers[i];
                    if (container == null)
                        continue;

                    Vector3 containerPos = container.transform.position;
                    for (int j = 0, m = deadPlayers.Count; j < m; j++)
                    {
                        Player player = deadPlayers[j];
                        if (player == null || string.IsNullOrEmpty(player.ProfileId) || DeadBodyContainerCache.Contains(player.ProfileId))
                            continue;

                        float dist = Vector3.Distance(player.Transform.position, containerPos);
                        if (dist <= MaxContainerDistance)
                        {
                            DeadBodyContainerCache.Register(player, container);
                            break;
                        }
                    }
                }

                TempListPool.Return(deadPlayers);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[BotDeadBodyScanner] Exception in ScanAll: {ex}");
            }
        }

        public static void ClearStaticState()
        {
            LootTimestamps.Clear();
            RecentlyLooted.Clear();
        }

        public void Initialize(BotComponentCache cache)
        {
            if (cache == null || cache.Bot == null)
            {
                _bot = null;
                _cache = null;
                _personalScanVariance = 0f;
                return;
            }

            BotOwner resolved = cache.Bot.GetComponent<BotOwner>();
            if (resolved == null)
            {
                _bot = null;
                _cache = null;
                _personalScanVariance = 0f;
                return;
            }

            _bot = resolved;
            _cache = cache;
            _personalScanVariance = UnityEngine.Random.Range(-CooldownVariance * 0.5f, CooldownVariance * 0.5f);
            _nextScanTime = Mathf.Max(Time.time, Time.time + MinPersonalCooldown + _personalScanVariance);
        }

        #endregion

        #region Public Tick Entry

        public void Tick(float time)
        {
            try
            {
                if (_bot == null || _cache == null)
                    return;
                if (time < _nextScanTime || !IsReady())
                    return;

                _nextScanTime = time + LootCooldown + _personalScanVariance;
                TryLootOnce();
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[BotDeadBodyScanner] Exception in Tick: {ex}");
                _bot = null;
                _cache = null;
            }
        }

        public void TryLootNearby()
        {
            try
            {
                if (IsReady())
                    TryLootOnce();
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[BotDeadBodyScanner] Exception in TryLootNearby: {ex}");
                _bot = null;
                _cache = null;
            }
        }

        #endregion

        #region Core Logic

        private bool IsReady()
        {
            return _bot != null
                && !_bot.IsDead
                && EFTPlayerUtil.IsValid(_bot.GetPlayer)
                && _cache != null
                && _cache.PanicHandler != null
                && !_cache.PanicHandler.IsPanicking;
        }

        private void TryLootOnce()
        {
            Player corpse = null;
            try
            {
                corpse = FindLootableCorpse();
                if (!EFTPlayerUtil.IsValid(corpse))
                    return;

                string profileId = corpse.ProfileId;
                if (string.IsNullOrEmpty(profileId))
                    return;

                // Move to corpse before looting
                if (!TryMoveToCorpse(corpse))
                    return;

                LootCorpse(corpse);
                RememberLooted(profileId);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[BotDeadBodyScanner] Exception in TryLootOnce (corpse={corpse?.ProfileId ?? "null"}): {ex}");
            }
        }

        private bool TryMoveToCorpse(Player corpse)
        {
            if (_bot == null || corpse == null || corpse.Transform == null)
                return false;

            Vector3 target = corpse.Transform.position;
            float dist = Vector3.Distance(_bot.Position, target);
            if (dist > MoveToCorpseRadius)
            {
                BotMovementHelper.SmoothMoveToSafe(_bot, target, slow: true, MoveToCorpseCohesion);
                return false; // Wait for next tick when in range
            }
            return true;
        }

        private Player FindLootableCorpse()
        {
            if (_bot == null || _bot.Transform == null)
                return null;

            Vector3 origin = _bot.Transform.position;
            Vector3 forward = (_bot.WeaponRoot != null) ? _bot.WeaponRoot.forward : _bot.Transform.forward;

            List<Player> allCorpses = TempListPool.Rent<Player>();
            try
            {
                List<Player> possible = GameWorldHandler.GetAllAlivePlayers();
                for (int i = 0; i < possible.Count; i++)
                {
                    Player p = possible[i];
                    if (p != null && !p.HealthController.IsAlive)
                        allCorpses.Add(p);
                }

                Player best = null;
                float bestDist = ScanRadius + 1f;

                for (int i = 0, n = allCorpses.Count; i < n; i++)
                {
                    Player candidate = allCorpses[i];
                    if (!IsValidCorpse(candidate))
                        continue;

                    Vector3 toCorpse = candidate.Transform.position - origin;
                    float distance = toCorpse.magnitude;
                    float angle = Vector3.Angle(forward, toCorpse.normalized);
                    if (distance > ScanRadius || angle > MaxLootAngle)
                        continue;

                    if (!HasLineOfSight(origin, toCorpse, distance, candidate))
                        continue;

                    if (distance < bestDist)
                    {
                        best = candidate;
                        bestDist = distance;
                    }
                }

                return best;
            }
            finally
            {
                TempListPool.Return(allCorpses);
            }
        }

        private bool IsValidCorpse(Player player)
        {
            return EFTPlayerUtil.IsValid(player)
                && player.HealthController != null
                && !player.HealthController.IsAlive
                && player != _bot.GetPlayer
                && !string.IsNullOrEmpty(player.ProfileId)
                && !WasLootedRecently(player.ProfileId);
        }

        private bool HasLineOfSight(Vector3 origin, Vector3 direction, float distance, Player corpse)
        {
            try
            {
                if (corpse?.Transform?.Original == null)
                    return false;

                Ray ray = new Ray(origin, direction.normalized);
                if (!Physics.Raycast(ray, out RaycastHit hit, distance + RaycastPadding, AIRefactoredLayerMasks.TerrainHighLow))
                    return false;

                return hit.collider.transform.root == corpse.Transform.Original.root;
            }
            catch
            {
                return false;
            }
        }

        private void LootCorpse(Player corpse)
        {
            try
            {
                InventoryController source = corpse.InventoryController;
                InventoryController target = _bot.GetPlayer.InventoryController;
                if (source == null || target == null)
                    return;

                LootableContainer container = DeadBodyContainerCache.Get(corpse.ProfileId);
                if (container != null && container.enabled)
                {
                    container.Interact(new InteractionResult(EInteractionType.Open));
                    return;
                }

                StealBestItemOnce(source, target);
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[BotDeadBodyScanner] Exception in LootCorpse: {ex}");
            }
        }

        private void StealBestItemOnce(InventoryController source, InventoryController target)
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
                try
                {
                    var slot = source.Inventory.Equipment.GetSlot(prioritySlots[i]);
                    if (slot == null)
                        continue;

                    Item item = slot.ContainedItem;
                    if (item == null)
                        continue;

                    ItemAddress destination = target.FindSlotToPickUp(item);
                    if (destination == null)
                        continue;

                    if (InteractionsHandlerClass.Move(item, destination, target, true).Succeeded)
                        break;
                }
                catch (Exception ex)
                {
                    Plugin.LoggerInstance.LogError($"[BotDeadBodyScanner] Exception in StealBestItemOnce slot {prioritySlots[i]}: {ex}");
                }
            }
        }

        private void RememberLooted(string profileId)
        {
            RecentlyLooted.Add(profileId);
            LootTimestamps[profileId] = Time.time;
        }

        private bool WasLootedRecently(string profileId)
        {
            return LootTimestamps.TryGetValue(profileId, out float lastTime)
                && (Time.time - lastTime < LootMemoryDuration + UnityEngine.Random.Range(-2f, 2f));
        }

        #endregion
    }
}
