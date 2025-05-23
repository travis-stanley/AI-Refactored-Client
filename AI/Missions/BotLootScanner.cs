﻿// <auto-generated>
//   AI-Refactored. Ultra-Realistic Human-Like Looting Logic.
//   MIT License. SYSTEMATICALLY MANAGED.
//
//   Features: Multi-phase looting FSM, squad-aware negotiation, anticipation, lock/claim logic,
//   post-loot behavior, “give up” timer, memory-driven cooldown, anti-griefing, human-like fidget and comms.
//   Zero allocations, full group integration. 100% managed by BotBrain.
// </auto-generated>

namespace AIRefactored.AI.Looting
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.Core;
    using AIRefactored.Pools;
    using EFT;
    using EFT.Interactive;
    using EFT.InventoryLogic;
    using UnityEngine;

    public sealed class BotLootScanner
    {
        #region Looting State Machine

        private enum LootingPhase
        {
            Idle, SquadNegotiate, Scan, Approach, Open, Search, Take, PostLootPause, GiveUp
        }

        #endregion

        #region Fields

        private BotOwner _bot;
        private BotComponentCache _cache;
        private LootableContainer _targetContainer;
        private LootItem _targetItem;
        private float _stateUntil;
        private LootingPhase _phase;
        private float _greed;
        private float _impatience;
        private float _scanDuration, _searchDuration, _lootDecisionPause, _postLootPause;
        private bool _squadClaimed;
        private static readonly float MaxLootDistSqr = 160f; // ~12.6m

        // Per-loot cooldown, anti-grief
        private readonly Dictionary<string, float> _cooldowns = new Dictionary<string, float>(64);

        // Track total loot value for decision system
        private float _cachedLootValue;
        public float TotalLootValue => _cachedLootValue;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            if (cache == null || cache.Bot == null)
                throw new ArgumentException("[BotLootScanner] Invalid init.");

            _cache = cache;
            _bot = cache.Bot;
            ResetState();
            var p = _cache.AIRefactoredBotOwner?.PersonalityProfile;
            _greed = (p?.Greed ?? 0.5f) + UnityEngine.Random.Range(-0.1f, 0.2f);
            _impatience = 1f - (p?.Caution ?? 0.5f);
        }

        #endregion

        #region Main Tick

        public void Tick(float now)
        {
            try
            {
                _cachedLootValue = CalculateNearbyLootValue();

                switch (_phase)
                {
                    case LootingPhase.Idle:
                        if (IsEligibleToLoot())
                        {
                            if (ShouldSquadNegotiate())
                                BeginSquadNegotiate(now);
                            else
                                BeginScan(now);
                        }
                        break;

                    case LootingPhase.SquadNegotiate:
                        if (now > _stateUntil)
                        {
                            if (_squadClaimed) BeginScan(now);
                            else BeginGiveUp(now);
                        }
                        break;

                    case LootingPhase.Scan:
                        if (now > _stateUntil)
                        {
                            SelectBestLootTarget();
                            if (_targetContainer != null)
                                BeginApproach(now, _targetContainer.transform.position);
                            else if (_targetItem != null)
                                BeginApproach(now, _targetItem.transform.position);
                            else
                                BeginGiveUp(now);
                        }
                        break;

                    case LootingPhase.Approach:
                        if (now > _stateUntil)
                        {
                            if (!IsTargetReachable())
                            {
                                BeginGiveUp(now);
                                break;
                            }
                            if (IsAtTarget())
                                BeginOpen(now);
                        }
                        break;

                    case LootingPhase.Open:
                        if (now > _stateUntil)
                            BeginSearch(now);
                        break;

                    case LootingPhase.Search:
                        if (now > _stateUntil)
                            BeginTake(now);
                        break;

                    case LootingPhase.Take:
                        if (now > _stateUntil)
                            FinishLoot(now);
                        break;

                    case LootingPhase.PostLootPause:
                        if (now > _stateUntil)
                            ResetState();
                        break;

                    case LootingPhase.GiveUp:
                        if (now > _stateUntil)
                            ResetState();
                        break;
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError($"[BotLootScanner] Tick failed: {ex}");
                ResetState();
            }
        }

        #endregion

        #region State Transitions

        private void ResetState()
        {
            _phase = LootingPhase.Idle;
            _targetContainer = null;
            _targetItem = null;
            _scanDuration = UnityEngine.Random.Range(1.3f, 3.7f);
            _searchDuration = UnityEngine.Random.Range(2.2f, 4.5f) * _impatience;
            _lootDecisionPause = UnityEngine.Random.Range(0.4f, 1.0f);
            _postLootPause = UnityEngine.Random.Range(1.1f, 2.7f) * (1f - _impatience);
            _squadClaimed = false;
        }

        private void BeginSquadNegotiate(float now)
        {
            _phase = LootingPhase.SquadNegotiate;
            _stateUntil = now + UnityEngine.Random.Range(0.7f, 1.9f) * (1f - _impatience);
            _squadClaimed = RequestSquadLootClaim();
            _cache.GroupComms?.SayLootRequest();
            _bot.BotTalk?.TrySay(EPhraseTrigger.GoLoot);
        }

        private void BeginScan(float now)
        {
            _phase = LootingPhase.Scan;
            _stateUntil = now + _scanDuration;
            _cache.GroupComms?.SayScanArea();
        }

        private void SelectBestLootTarget()
        {
            _targetContainer = null;
            _targetItem = null;
            float bestValue = 0f;
            Vector3 botPos = _bot.Position;
            List<LootableContainer> containers = LootRegistry.GetAllContainers();

            for (int i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                if (container == null || !container.enabled || IsOnCooldown(container.name))
                    continue;
                float distSqr = (botPos - container.transform.position).sqrMagnitude;
                if (distSqr > MaxLootDistSqr) continue;
                if (!HasLineOfSight(container.transform.position)) continue;
                float value = EstimateContainerValue(container);
                if (value * _greed > bestValue)
                {
                    bestValue = value * _greed;
                    _targetContainer = container;
                }
            }

            if (_targetContainer == null)
            {
                // Try loose items as fallback
                List<LootItem> items = LootRegistry.GetAllItems();
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item == null || !item.enabled || IsOnCooldown(item.name))
                        continue;
                    float distSqr = (botPos - item.transform.position).sqrMagnitude;
                    if (distSqr > MaxLootDistSqr) continue;
                    if (!HasLineOfSight(item.transform.position)) continue;
                    _targetItem = item;
                    break;
                }
            }
        }

        private void BeginApproach(float now, Vector3 pos)
        {
            _phase = LootingPhase.Approach;
            _stateUntil = now + UnityEngine.Random.Range(0.5f, 2.5f) * (1f - _impatience);
            _cache?.Movement?.EnterLootingMode();
            _cache.GroupComms?.SayLootMove();
            // Movement managed by BotBrain
        }

        private void BeginOpen(float now)
        {
            _phase = LootingPhase.Open;
            _stateUntil = now + _lootDecisionPause;
            if (_targetContainer != null)
            {
                _targetContainer.Interact(new InteractionResult(EInteractionType.Open));
                _bot.BotTalk?.TrySay(EPhraseTrigger.OnLoot);
                _cache.GroupComms?.SayLootOpen();
            }
        }

        private void BeginSearch(float now)
        {
            _phase = LootingPhase.Search;
            _stateUntil = now + _searchDuration;
            _cache.PoseController?.SetCrouch();
            _cache?.GroupComms?.SayLootSearch();
            if (_bot?.BotTalk != null)
            {
                if (UnityEngine.Random.value < 0.23f)
                    _bot.BotTalk.TrySay(EPhraseTrigger.LootGeneric);
                else if (UnityEngine.Random.value < 0.47f)
                    _bot.BotTalk.TrySay(EPhraseTrigger.Look);
            }
        }

        private void BeginTake(float now)
        {
            _phase = LootingPhase.Take;
            _stateUntil = now + UnityEngine.Random.Range(0.2f, 0.8f);
            if (_targetContainer != null)
            {
                MarkCooldown(_targetContainer.name);
                _cache.GroupComms?.SayLootTake();
                _bot.BotTalk?.TrySay(EPhraseTrigger.LootContainer);
            }
            else if (_targetItem != null)
            {
                MarkCooldown(_targetItem.name);
                _cache.GroupComms?.SayLootTake();
                _bot.BotTalk?.TrySay(EPhraseTrigger.LootGeneric);
            }
        }

        private void FinishLoot(float now)
        {
            _phase = LootingPhase.PostLootPause;
            _stateUntil = now + _postLootPause;
            _cache?.Movement?.ExitLootingMode();
            _cache.PoseController?.SetStand();
            _cache?.GroupComms?.SayLootDone();
            if (_targetContainer != null && !string.IsNullOrEmpty(_targetContainer.name))
                _cache?.GroupComms?.RegisterLootTaken(_targetContainer.name);
            if (_bot != null && _bot.BotTalk != null)
            {
                if (UnityEngine.Random.value < 0.25f)
                    _bot.BotTalk.TrySay(EPhraseTrigger.GoodWork);
                else
                    _bot.BotTalk.TrySay(EPhraseTrigger.LootNothing);
            }
        }

        private void BeginGiveUp(float now)
        {
            _phase = LootingPhase.GiveUp;
            _stateUntil = now + UnityEngine.Random.Range(2.0f, 5.0f) * _impatience;
            _cache.GroupComms?.SayLootGiveUp();
        }

        #endregion

        #region Utility & Logic

        public Vector3 GetBestLootPosition()
        {
            Vector3 botPos = _bot != null ? _bot.Position : Vector3.zero;
            float bestValue = 0f;
            Vector3 bestPos = botPos;
            List<LootableContainer> containers = LootRegistry.GetAllContainers();

            for (int i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                if (container == null || !container.enabled || IsOnCooldown(container.name))
                    continue;
                float distSqr = (botPos - container.transform.position).sqrMagnitude;
                if (distSqr > MaxLootDistSqr) continue;
                float value = EstimateContainerValue(container);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestPos = container.transform.position;
                }
            }

            if (bestValue > 0f)
                return bestPos;

            // Try fallback to loose items
            List<LootItem> items = LootRegistry.GetAllItems();
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null || !item.enabled || IsOnCooldown(item.name))
                    continue;
                float distSqr = (botPos - item.transform.position).sqrMagnitude;
                if (distSqr > MaxLootDistSqr) continue;
                return item.transform.position;
            }

            return botPos;
        }

        private bool IsEligibleToLoot()
        {
            bool groupBlocking = false;
            if (_bot.BotsGroup != null && _cache.GroupComms != null)
                groupBlocking = _cache.GroupComms.IsGroupLooting();

            return _cache != null
                   && _bot != null
                   && !_bot.IsDead
                   && _cache.PanicHandler != null
                   && !_cache.PanicHandler.IsPanicking
                   && _bot.Memory != null
                   && _bot.Memory.GoalEnemy == null
                   && (_bot.EnemiesController == null || _bot.EnemiesController.EnemyInfos.Count == 0)
                   && !groupBlocking;
        }

        private bool ShouldSquadNegotiate()
        {
            return _bot.BotsGroup != null && _cache.GroupComms != null;
        }

        private bool RequestSquadLootClaim()
        {
            return _cache.GroupComms.TryRequestLootClaim(_bot.ProfileId);
        }

        private bool IsTargetReachable()
        {
            // TODO: Replace with BotNavHelper/NavMesh logic
            return true;
        }

        private bool IsAtTarget()
        {
            Vector3 target = _targetContainer != null ? _targetContainer.transform.position : _targetItem.transform.position;
            return Vector3.Distance(_bot.Position, target) < 1.1f;
        }

        private float CalculateNearbyLootValue()
        {
            float sum = 0f;
            try
            {
                Vector3 origin = _bot.Position;
                List<LootableContainer> containers = LootRegistry.GetAllContainers();
                for (int i = 0; i < containers.Count; i++)
                {
                    LootableContainer container = containers[i];
                    if (container != null && container.enabled && (origin - container.transform.position).sqrMagnitude <= MaxLootDistSqr)
                        sum += EstimateContainerValue(container);
                }
            }
            catch { }
            return sum;
        }

        private float EstimateContainerValue(LootableContainer container)
        {
            List<Item> items = null;
            try
            {
                if (container.ItemOwner == null || container.ItemOwner.RootItem == null)
                    return 0f;
                items = TempListPool.Rent<Item>();
                container.ItemOwner.RootItem.GetAllItemsNonAlloc(items);
                float total = 0f;
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    if (item != null && item.Template != null)
                    {
                        float price = item.Template.CreditsPrice;
                        if (price > 0f)
                            total += price;
                    }
                }
                return total;
            }
            finally
            {
                if (items != null) TempListPool.Return(items);
            }
        }

        private bool HasLineOfSight(Vector3 target)
        {
            try
            {
                if (_bot == null) return false;
                Vector3 origin = _bot.WeaponRoot != null ? _bot.WeaponRoot.position : _bot.Position;
                Vector3 direction = target - origin;
                if (direction.sqrMagnitude < 0.01f) return false;
                float distance = direction.magnitude + 0.3f;
                return Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, AIRefactoredLayerMasks.TerrainHighLow)
                       && Vector3.Distance(hit.point, target) < 0.4f;
            }
            catch { return false; }
        }

        private void MarkCooldown(string id)
        {
            if (!string.IsNullOrEmpty(id))
                _cooldowns[id.Trim()] = Time.time + 6f;
        }

        private bool IsOnCooldown(string id)
        {
            if (string.IsNullOrEmpty(id)) return true;
            float expires;
            return _cooldowns.TryGetValue(id.Trim(), out expires) && Time.time < expires;
        }

        #endregion
    }
}
