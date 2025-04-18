#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Hotspots;
using AIRefactored.Core;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Missions
{
    /// <summary>
    /// Controls high-level mission behavior for bots: loot, fight, or quest.
    /// Used by BotAsyncThinker to simulate long-term goals and behavior chains.
    /// </summary>
    public class BotMissionSystem : MonoBehaviour
    {
        #region Enums

        public enum MissionType { Loot, Fight, Quest }

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotGroupSyncCoordinator? _group;
        private CombatStateMachine? _combat;
        private BotPersonalityProfile? _personality;

        private Vector3 _currentObjective;
        private MissionType _missionType;

        private float _lastUpdate;
        private const float _objectiveCooldown = 15f;
        private const float REACHED_THRESHOLD = 6f;
        private const float SQUAD_COHESION_RANGE = 10f;

        private bool _isLooting = false;
        private bool _readyToExtract = false;
        private bool _waitForGroup = false;
        private bool _forcedMission = false;
        private bool _lootComplete = false;
        private bool _fightComplete = false;

        private readonly List<LootableContainer> _lootContainers = new List<LootableContainer>();
        private readonly System.Random _rng = new System.Random();

        #endregion

        #region Initialization

        public void Init(BotOwner bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            if (_bot.GetPlayer?.IsYourPlayer == true)
                return;

            _group = _bot.GetPlayer?.GetComponent<BotGroupSyncCoordinator>();
            _combat = _bot.GetPlayer?.GetComponent<CombatStateMachine>();
            _personality = BotRegistry.Get(_bot.Profile.Id);

            CacheLootZones();

            if (!_forcedMission)
                PickMission();
        }

        public void SetForcedMission(MissionType mission)
        {
            _missionType = mission;
            _forcedMission = true;
        }

        #endregion

        #region Main Tick

        public void ManualTick(float time)
        {
            if (_bot == null || _bot.GetPlayer == null || !_bot.GetPlayer.HealthController.IsAlive)
                return;

            if (_bot.GetPlayer.IsYourPlayer || (_combat?.IsInCombatState() == true))
                return;

            if (_waitForGroup && !IsGroupAligned())
                return;

            if (time - _lastUpdate > _objectiveCooldown)
            {
                EvaluateMission();
                _lastUpdate = time;
            }

            if (Vector3.Distance(_bot.Position, _currentObjective) < REACHED_THRESHOLD)
            {
                OnObjectiveReached();
            }
        }

        #endregion

        #region Mission Evaluation

        private void PickMission()
        {
            if (_personality == null)
            {
                _missionType = MissionType.Loot;
                _currentObjective = GetBestLootZone();
                return;
            }

            bool isPmc = _bot.Profile.Info.Side == EPlayerSide.Usec || _bot.Profile.Info.Side == EPlayerSide.Bear;
            MissionBias bias = _personality.PreferredMission;

            _missionType = bias switch
            {
                MissionBias.Quest => isPmc ? MissionType.Quest : MissionType.Loot,
                MissionBias.Fight => MissionType.Fight,
                MissionBias.Loot => MissionType.Loot,
                _ => RandomizeMissionType(isPmc)
            };

            _currentObjective = GetInitialObjective(_missionType);
            BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
        }

        private MissionType RandomizeMissionType(bool isPmc)
        {
            int roll = _rng.Next(0, 100);
            if (roll < 30) return MissionType.Loot;
            if (roll < 65) return MissionType.Fight;
            return isPmc ? MissionType.Quest : MissionType.Loot;
        }

        private Vector3 GetInitialObjective(MissionType mission)
        {
            return mission switch
            {
                MissionType.Loot => GetBestLootZone(),
                MissionType.Fight => GetRandomZone(MissionType.Fight),
                MissionType.Quest => HotspotSystem.GetRandomHotspot(_bot!),
                _ => _bot?.Position ?? Vector3.zero
            };
        }

        private void EvaluateMission()
        {
            if (_bot == null || _bot.GetPlayer?.IsYourPlayer == true)
                return;

            switch (_missionType)
            {
                case MissionType.Loot:
                    if (!_readyToExtract && IsBackpackFull(_bot))
                    {
                        _readyToExtract = true;
                        Say(EPhraseTrigger.OnFight);
                        _missionType = MissionType.Fight;
                        _currentObjective = GetRandomZone(MissionType.Fight);
                        BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                    }
                    else if (!_lootComplete)
                    {
                        _currentObjective = GetBestLootZone();
                        BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                    }
                    break;

                case MissionType.Fight:
                    if (!_fightComplete)
                    {
                        _fightComplete = true;
                        _missionType = MissionType.Quest;
                        _currentObjective = HotspotSystem.GetRandomHotspot(_bot);
                        BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                    }
                    break;

                case MissionType.Quest:
                    TryExtract();
                    break;
            }

            _waitForGroup = !IsGroupAligned();
        }

        private void OnObjectiveReached()
        {
            if (_missionType == MissionType.Loot && !_isLooting)
            {
                _isLooting = true;
                Say(EPhraseTrigger.OnLoot);
                _bot?.Mover?.Stop();

                UnityMainThreadDispatcher.Enqueue(() =>
                {
                    if (_bot != null)
                        BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                });

                return;
            }

            if (_readyToExtract && _missionType == MissionType.Quest)
            {
                Say(EPhraseTrigger.MumblePhrase);
                _bot?.Deactivate();
                _bot?.GetPlayer?.gameObject.SetActive(false);
            }
        }

        #endregion

        #region Mission Goals

        private void CacheLootZones()
        {
            _lootContainers.Clear();
            foreach (var container in GameObject.FindObjectsOfType<LootableContainer>())
            {
                if (container != null)
                    _lootContainers.Add(container);
            }
        }

        private Vector3 GetBestLootZone()
        {
            float highestValue = 0f;
            Vector3 bestPos = _bot?.Position ?? Vector3.zero;

            for (int i = 0; i < _lootContainers.Count; i++)
            {
                var container = _lootContainers[i];
                float value = EstimateContainerValue(container);
                if (value > highestValue)
                {
                    highestValue = value;
                    bestPos = container.transform.position;
                }
            }

            return bestPos;
        }

        private float EstimateContainerValue(LootableContainer container)
        {
            if (container?.ItemOwner?.RootItem == null)
                return 0f;

            float total = 0f;
            foreach (var item in container.ItemOwner.RootItem.GetAllItems())
            {
                if (item?.Template != null && item.Template.CreditsPrice > 0)
                    total += item.Template.CreditsPrice;
            }

            return total;
        }

        private Vector3 GetRandomZone(MissionType type)
        {
            if (type == MissionType.Fight)
            {
                var zones = GameObject.FindObjectsOfType<BotZone>();
                if (zones.Length > 0)
                {
                    var randomZone = zones[_rng.Next(0, zones.Length)];
                    return randomZone.transform.position;
                }
            }

            return _bot?.Position ?? Vector3.zero;
        }

        private void TryExtract()
        {
            if (_bot?.GetPlayer?.IsYourPlayer == true)
                return;

            ExfiltrationPoint? closest = null;
            float minDist = float.MaxValue;

            foreach (var point in GameObject.FindObjectsOfType<ExfiltrationPoint>())
            {
                if (point == null || point.Status != EExfiltrationStatus.RegularMode)
                    continue;

                float dist = Vector3.Distance(_bot.Position, point.transform.position);
                if (dist < minDist)
                {
                    closest = point;
                    minDist = dist;
                }
            }

            if (closest != null)
            {
                BotMovementHelper.SmoothMoveTo(_bot, closest.transform.position);
                Say(EPhraseTrigger.ExitLocated);
            }
            else
            {
                Say(EPhraseTrigger.MumblePhrase);
            }
        }

        #endregion

        #region Group Check

        private bool IsGroupAligned()
        {
            if (_group == null)
                return true;

            var teammates = _group.GetTeammates();
            if (teammates.Count == 0)
                return true;

            int nearby = 0;
            for (int i = 0; i < teammates.Count; i++)
            {
                var mate = teammates[i];
                if (mate != null && Vector3.Distance(mate.Position, _bot.Position) < SQUAD_COHESION_RANGE)
                    nearby++;
            }

            return nearby >= Mathf.CeilToInt(teammates.Count * 0.6f);
        }

        private bool IsBackpackFull(BotOwner bot)
        {
            var inv = bot.GetPlayer?.Inventory;
            var slot = inv?.Equipment?.GetSlot(EquipmentSlot.Backpack);
            var backpack = slot?.ContainedItem;

            if (backpack == null)
                return false;

            int count = 0;
            foreach (var item in backpack.GetAllItems())
            {
                if (item != null)
                    count++;
                if (count >= 40)
                    return true;
            }

            return false;
        }

        #endregion

        #region VO

        private void Say(EPhraseTrigger phrase)
        {
            try
            {
                _bot?.GetPlayer?.Say(phrase);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIRefactored-Mission] Voice failed: {ex.Message}");
            }
        }

        #endregion
    }
}
