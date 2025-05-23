﻿// <auto-generated>
//   AI-Refactored. Human-Realistic Loot Decision System.
//   MIT License. SYSTEMATICALLY MANAGED.
//   All logic 100% managed by BotBrain. All failures locally isolated.
// </auto-generated>

namespace AIRefactored.AI.Looting
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.Pools;
    using BepInEx.Logging;
    using EFT;
    using EFT.Interactive;
    using EFT.InventoryLogic;
    using UnityEngine;

    /// <summary>
    /// Dynamic human-like looting decision system.
    /// Considers squad, proximity, recent threats, value, memory, and group comms.
    /// Bulletproof fallback: errors never break squad/AIRefactored, disables only local looting.
    /// </summary>
    public sealed class BotLootDecisionSystem
    {
        #region Constants

        private const float MaxLootDistance = 22f;
        private const float HighValueThreshold = 25000f;
        private const float CooldownTime = 14f;
        private const float GroupClaimCooldown = 17f;
        private const float SquadLootBlockRadius = 4.2f;

        #endregion

        #region Fields

        private BotComponentCache _cache;
        private BotOwner _bot;
        private BotGroupComms _groupComms;
        private float _nextLootTime;
        private float _lastSquadClaimTime;
        private readonly HashSet<string> _recentLooted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static ManualLogSource Logger => Plugin.LoggerInstance;

        // Squad claim state
        private readonly Dictionary<string, float> _squadLootClaims = new Dictionary<string, float>(16);
        private string _currentSquadClaimId;
        private float _currentSquadClaimUntil;
        private bool _isActive = true;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            if (cache == null || cache.Bot == null)
            {
                _isActive = false;
                Logger.LogError("[BotLootDecisionSystem] Initialization failed: cache or bot is null. Disabling looting logic for this bot.");
                return;
            }

            _cache = cache;
            _bot = cache.Bot;
            _groupComms = cache.GroupComms;
            _isActive = true;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Should this bot attempt to loot now? Considers panic, squad, nearby threats, loot value, group claims, personality impatience.
        /// </summary>
        public bool ShouldLootNow()
        {
            if (!_isActive || _bot == null || _bot.IsDead)
                return false;

            if (Time.time < _nextLootTime)
                return false;

            try
            {
                // Suppress looting if panicking or recently threatened
                if (_cache.PanicHandler != null && _cache.PanicHandler.IsPanicking)
                    return false;
                if (_bot.Memory != null && _bot.Memory.GoalEnemy != null)
                    return false;
                if (_bot.EnemiesController != null && _bot.EnemiesController.EnemyInfos != null && _bot.EnemiesController.EnemyInfos.Count > 0)
                    return false;

                // Avoid looting if a squadmate is fighting nearby or already looting close
                if (_bot.BotsGroup != null && _bot.BotsGroup.MembersCount > 1)
                {
                    for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
                    {
                        var mate = _bot.BotsGroup.Member(i);
                        if (mate == null || mate == _bot || mate.IsDead) continue;
                        float dist = Vector3.Distance(_bot.Position, mate.Position);
                        if (mate.Memory != null && mate.Memory.GoalEnemy != null && dist < 16f)
                            return false;
                        if (IsSquadmateLootingSameContainer(mate, out string lootedId) && dist < SquadLootBlockRadius)
                        {
                            // If this bot is impatient, may still try with random chance
                            var profile = _cache.PersonalityProfile ?? BotPersonalityProfile.Default;
                            if (profile.Greed > 0.7f && UnityEngine.Random.value < 0.18f)
                            {
                                _groupComms?.Say(EPhraseTrigger.OnFirstContact);
                                continue;
                            }
                            // Otherwise, respect the squadmate claim
                            _groupComms?.Say(EPhraseTrigger.HoldPosition);
                            return false;
                        }
                    }
                }

                // Only loot if a valuable container is nearby
                return _cache.LootScanner != null && _cache.LootScanner.TotalLootValue >= HighValueThreshold;
            }
            catch (Exception ex)
            {
                _isActive = false;
                Logger.LogError($"[BotLootDecisionSystem] ShouldLootNow() failed: {ex}");
                return false;
            }
        }

        /// <summary>
        /// Returns the best loot destination considering squad, memory, cooldown, value, safety, and personality.
        /// </summary>
        public Vector3 GetLootDestination()
        {
            if (!_isActive || _cache == null || _cache.LootScanner == null || _bot == null)
                return Vector3.zero;

            try
            {
                float bestValue = 0f;
                Vector3 bestPoint = Vector3.zero;
                string bestId = null;
                float closestDist = float.MaxValue;

                List<LootableContainer> containers = LootRegistry.GetAllContainers();
                if (containers == null)
                    return Vector3.zero;

                var profile = _cache.PersonalityProfile ?? BotPersonalityProfile.Default;

                for (int i = 0; i < containers.Count; i++)
                {
                    LootableContainer container = containers[i];
                    if (container == null || !container.enabled || container.transform == null)
                        continue;

                    string lootId = container.Id;
                    if (string.IsNullOrWhiteSpace(lootId) || WasRecentlyLooted(lootId))
                        continue;

                    if (IsContainerClaimedBySquad(lootId))
                        continue;

                    Vector3 pos = container.transform.position;
                    float dist = Vector3.Distance(_bot.Position, pos);
                    if (dist > MaxLootDistance)
                        continue;

                    float value = EstimateValue(container);

                    // Impatient/greedy bots are more likely to pick close or even lower value containers
                    float greedBias = Mathf.Lerp(0.75f, 1.15f, profile.Greed);

                    value *= greedBias;

                    // Prefer closer containers if values are tied or similar
                    if (value > bestValue || (Mathf.Approximately(value, bestValue) && dist < closestDist))
                    {
                        bestValue = value;
                        bestPoint = pos;
                        bestId = lootId;
                        closestDist = dist;
                    }
                }

                if (bestValue > 0f && bestId != null)
                {
                    ClaimContainerForSquad(bestId);
                    return bestPoint;
                }

                return Vector3.zero;
            }
            catch (Exception ex)
            {
                _isActive = false;
                Logger.LogError($"[BotLootDecisionSystem] GetLootDestination() failed: {ex}");
                return Vector3.zero;
            }
        }

        /// <summary>
        /// Mark this container as looted and set internal/squad cooldowns.
        /// </summary>
        public void MarkLooted(string lootId)
        {
            if (!_isActive || string.IsNullOrWhiteSpace(lootId))
                return;

            try
            {
                string id = lootId.Trim();
                _recentLooted.Add(id);
                _nextLootTime = Time.time + CooldownTime;
                ReleaseContainerClaim(id);
            }
            catch (Exception ex)
            {
                _isActive = false;
                Logger.LogError($"[BotLootDecisionSystem] MarkLooted() failed: {ex}");
            }
        }

        /// <summary>
        /// Checks if this bot has recently looted the given container.
        /// </summary>
        public bool WasRecentlyLooted(string lootId)
        {
            if (!_isActive || string.IsNullOrWhiteSpace(lootId))
                return false;

            try
            {
                return _recentLooted.Contains(lootId.Trim());
            }
            catch (Exception ex)
            {
                _isActive = false;
                Logger.LogError($"[BotLootDecisionSystem] WasRecentlyLooted() failed: {ex}");
                return false;
            }
        }

        #endregion

        #region Internal Squad/Group Coordination

        // In real implementation, should be managed by squad AI; here it's local.
        private void ClaimContainerForSquad(string lootId)
        {
            if (string.IsNullOrWhiteSpace(lootId))
                return;
            _squadLootClaims[lootId] = Time.time + GroupClaimCooldown;
            _currentSquadClaimId = lootId;
            _currentSquadClaimUntil = Time.time + GroupClaimCooldown;
            _groupComms?.SayLootRequest();
            _lastSquadClaimTime = Time.time;
        }

        private void ReleaseContainerClaim(string lootId)
        {
            if (string.IsNullOrWhiteSpace(lootId))
                return;
            _squadLootClaims.Remove(lootId);
            if (_currentSquadClaimId == lootId)
                _currentSquadClaimId = null;
            _groupComms?.SayLootDone();
        }

        private bool IsContainerClaimedBySquad(string lootId)
        {
            if (string.IsNullOrWhiteSpace(lootId))
                return false;
            float until;
            return _squadLootClaims.TryGetValue(lootId, out until) && Time.time < until;
        }

        private bool IsSquadmateLootingSameContainer(BotOwner mate, out string lootedId)
        {
            lootedId = null;
            if (mate == null || mate.IsDead)
                return false;

            var mateCache = mate.GetComponent<BotComponentCache>();
            if (mateCache?.LootDecisionSystem == null)
                return false;

            foreach (var kv in mateCache.LootDecisionSystem._squadLootClaims)
            {
                if (Time.time < kv.Value)
                {
                    lootedId = kv.Key;
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Internal Helpers

        private static float EstimateValue(LootableContainer container)
        {
            if (container == null || container.ItemOwner == null || container.ItemOwner.RootItem == null)
                return 0f;

            float total = 0f;
            List<Item> items = null;

            try
            {
                items = TempListPool.Rent<Item>();
                container.ItemOwner.RootItem.GetAllItemsNonAlloc(items);

                for (int i = 0; i < items.Count; i++)
                {
                    Item item = items[i];
                    if (item != null && item.Template != null && item.Template.CreditsPrice > 0f)
                        total += item.Template.CreditsPrice;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"[BotLootDecisionSystem] EstimateValue() failed: {ex}");
                return 0f;
            }
            finally
            {
                if (items != null)
                    TempListPool.Return(items);
            }

            return total;
        }

        #endregion
    }
}
