﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   Please follow strict StyleCop, ReSharper, and AI-Refactored code standards for all modifications.
// </auto-generated>

#nullable enable

namespace AIRefactored.AI.Looting
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.Core;
    using AIRefactored.Runtime;
    using EFT;
    using EFT.Interactive;
    using EFT.InventoryLogic;
    using UnityEngine;

    /// <summary>
    /// Scans for nearby loot targets and selects high-value options for tactical looting.
    /// </summary>
    public sealed class BotLootScanner
    {
        #region Constants

        private const float ScanInterval = 1.6f;
        private const float ScanRadius = 12f;
        private const float HighestValueRadius = 24f;
        private const float CooldownSeconds = 6f;
        private const float MaxAngle = 120f;
        private const float StaleValueResetDelay = 10f;

        #endregion

        #region Fields

        private readonly Dictionary<string, float> _lootCooldowns = new Dictionary<string, float>(64);

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private float _nextScanTime;
        private float _cachedValue;
        private float _lastValueUpdate;

        #endregion

        #region Properties

        public float TotalLootValue => this._cachedValue;

        #endregion

        #region Public Methods

        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache;
            this._bot = cache.Bot;
        }

        public void Tick(float time)
        {
            if (!this.CanEvaluate(time))
            {
                return;
            }

            this._nextScanTime = time + ScanInterval;

            if (this.CanLoot())
            {
                this.TryLootNearby();
                this._cachedValue = this.EvaluateNearbyLootValue();
                this._lastValueUpdate = time;
            }

            if (this._cachedValue <= 0f && time - this._lastValueUpdate > StaleValueResetDelay)
            {
                this._cachedValue = 0f;
            }
        }

        public Vector3 GetHighestValueLootPoint()
        {
            if (this._bot == null || FikaHeadlessDetector.IsHeadless)
            {
                return Vector3.zero;
            }

            float bestValue = 0f;
            Vector3 bestPoint = this._bot.Position;

            List<LootableContainer> containers = LootRegistry.GetAllContainers();
            for (int i = 0; i < containers.Count; i++)
            {
                LootableContainer c = containers[i];
                if (c == null || !c.enabled)
                {
                    continue;
                }

                float dist = Vector3.Distance(this._bot.Position, c.transform.position);
                if (dist > HighestValueRadius || !this.CanSee(c.transform.position))
                {
                    continue;
                }

                float value = this.EstimateValue(c);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestPoint = c.transform.position;
                }
            }

            return bestPoint;
        }

        public void TryLootNearby()
        {
            if (!this.CanLoot() || this._bot == null)
            {
                return;
            }

            LootableContainer? body = DeadBodyContainerCache.Get(this._bot.ProfileId);
            if (body != null && body.enabled && !this.IsOnCooldown(body.name))
            {
                float dist = Vector3.Distance(this._bot.Position, body.transform.position);
                if (dist <= ScanRadius && this.CanSee(body.transform.position))
                {
                    this.MarkCooldown(body.name);
                    this._cache?.Movement?.EnterLootingMode();
                    body.Interact(new InteractionResult(EInteractionType.Open));
                    this._cache?.Movement?.ExitLootingMode();
                    AIRefactoredController.Logger.LogInfo($"[BotLootScanner] Looted dead body: {body.name}");
                    return;
                }
            }

            List<LootableContainer> containers = LootRegistry.GetAllContainers();
            for (int i = 0; i < containers.Count; i++)
            {
                LootableContainer c = containers[i];
                if (c == null || !c.enabled || this.IsOnCooldown(c.name))
                {
                    continue;
                }

                float dist = Vector3.Distance(this._bot.Position, c.transform.position);
                if (dist > ScanRadius || !this.CanSee(c.transform.position))
                {
                    continue;
                }

                this.MarkCooldown(c.name);
                this._cache?.Movement?.EnterLootingMode();
                c.Interact(new InteractionResult(EInteractionType.Open));
                this._cache?.Movement?.ExitLootingMode();
                AIRefactoredController.Logger.LogInfo($"[BotLootScanner] Looted container: {c.name}");
                return;
            }

            List<LootItem> items = LootRegistry.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                LootItem item = items[i];
                if (item == null || !item.enabled || this.IsOnCooldown(item.name))
                {
                    continue;
                }

                float dist = Vector3.Distance(this._bot.Position, item.transform.position);
                if (dist > ScanRadius || !this.CanSee(item.transform.position))
                {
                    continue;
                }

                this.MarkCooldown(item.name);
                this._cache?.Movement?.EnterLootingMode();
                this._cache?.Movement?.ExitLootingMode();
                AIRefactoredController.Logger.LogInfo($"[BotLootScanner] Detected item: {item.name}");
                return;
            }
        }

        #endregion

        #region Private Helpers

        private bool CanEvaluate(float time)
        {
            return this._bot != null
                && !this._bot.IsDead
                && time >= this._nextScanTime
                && !FikaHeadlessDetector.IsHeadless;
        }

        private bool CanLoot()
        {
            return this._bot != null
                && !this._bot.IsDead
                && this._cache?.PanicHandler?.IsPanicking != true
                && this._bot.Memory?.GoalEnemy == null
                && this._bot.EnemiesController?.EnemyInfos.Count == 0;
        }

        private bool CanSee(Vector3 target)
        {
            if (this._bot == null)
            {
                return false;
            }

            Vector3 origin = this._bot.WeaponRoot.position;
            Vector3 dir = target - origin;
            float angle = Vector3.Angle(this._bot.WeaponRoot.forward, dir);

            if (angle > MaxAngle)
            {
                return false;
            }

            if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dir.magnitude + 0.3f, AIRefactoredLayerMasks.HighPolyWithTerrainMaskAI))
            {
                return Vector3.Distance(hit.point, target) < 0.4f;
            }

            return true;
        }

        private float EstimateValue(LootableContainer container)
        {
            if (container.ItemOwner?.RootItem == null)
            {
                return 0f;
            }

            float value = 0f;
            List<Item> items = new List<Item>(container.ItemOwner.RootItem.GetAllItems());
            for (int i = 0; i < items.Count; i++)
            {
                Item item = items[i];
                if (item?.Template?.CreditsPrice > 0)
                {
                    value += item.Template.CreditsPrice;
                }
            }

            return value;
        }

        private float EvaluateNearbyLootValue()
        {
            if (this._bot == null)
            {
                return 0f;
            }

            float totalValue = 0f;
            List<LootableContainer> containers = LootRegistry.GetAllContainers();
            for (int i = 0; i < containers.Count; i++)
            {
                LootableContainer c = containers[i];
                if (c == null || !c.enabled)
                {
                    continue;
                }

                float dist = Vector3.Distance(this._bot.Position, c.transform.position);
                if (dist > ScanRadius)
                {
                    continue;
                }

                totalValue += this.EstimateValue(c);
            }

            return totalValue;
        }

        private bool IsOnCooldown(string id)
        {
            return !string.IsNullOrWhiteSpace(id)
                && this._lootCooldowns.TryGetValue(id.Trim(), out float t)
                && Time.time < t;
        }

        private void MarkCooldown(string id)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                this._lootCooldowns[id.Trim()] = Time.time + CooldownSeconds;
            }
        }

        #endregion
    }
}
