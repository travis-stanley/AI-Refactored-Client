#nullable enable

using System;
using System.Collections.Generic;
using UnityEngine;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using EFT.Communications;
using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;

namespace AIRefactored.AI.Missions
{
    public class BotMissionSystem : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotGroupSyncCoordinator? _group;
        private Vector3 _currentObjective;
        private MissionType _missionType;

        private float _lastUpdate;
        private const float _objectiveCooldown = 15f;

        private bool _isLooting = false;
        private bool _readyToExtract = false;
        private bool _waitForGroup = false;
        private bool _forcedMission = false;
        private bool _lootComplete = false;
        private bool _fightComplete = false;

        private const float REACHED_THRESHOLD = 6f;
        private const float SQUAD_COHESION_RANGE = 10f;

        private readonly List<LootableContainer> _lootContainers = new();
        private readonly List<Vector3> _hotZones = new();
        private readonly List<Vector3> _questZones = new();

        private readonly System.Random _rng = new();

        public enum MissionType { Loot, Fight, Quest }

        public void Init(BotOwner bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _group = _bot.GetPlayer?.GetComponent<BotGroupSyncCoordinator>();
            CacheMapZones();

            if (!_forcedMission)
                PickMission();
        }

        public void SetForcedMission(MissionType mission)
        {
            _missionType = mission;
            _forcedMission = true;
        }

        private void Update()
        {
            if (_bot == null || _bot.GetPlayer == null || !_bot.GetPlayer.HealthController.IsAlive)
                return;

            if (_waitForGroup && !IsGroupAligned())
                return;

            if (Time.time - _lastUpdate > _objectiveCooldown)
            {
                EvaluateMission();
                _lastUpdate = Time.time;
            }

            if (Vector3.Distance(_bot.Position, _currentObjective) < REACHED_THRESHOLD)
            {
                OnObjectiveReached();
            }
        }

        private void CacheMapZones()
        {
            _lootContainers.Clear();
            _hotZones.Clear();
            _questZones.Clear();

            foreach (var container in GameObject.FindObjectsOfType<LootableContainer>())
                _lootContainers.Add(container);

            foreach (var zone in GameObject.FindObjectsOfType<BotZone>())
                _hotZones.Add(zone.transform.position);

            _questZones.AddRange(_hotZones);
        }

        private void PickMission()
        {
            _missionType = MissionType.Loot;
            _currentObjective = GetBestLootZone();
            _bot?.GoToPoint(_currentObjective, false);
        }

        private void EvaluateMission()
        {
            if (_bot == null)
                return;

            if (_missionType == MissionType.Loot)
            {
                if (!_readyToExtract && InventoryUtil.IsBackpackFull(_bot))
                {
                    _readyToExtract = true;
                    Say(EPhraseTrigger.OnFight);
                    _missionType = MissionType.Fight;
                    _currentObjective = GetRandomZone(MissionType.Fight);
                    _bot.GoToPoint(_currentObjective, false);
                }
                else if (!_lootComplete)
                {
                    _currentObjective = GetBestLootZone();
                    _bot.GoToPoint(_currentObjective, false);
                }
            }
            else if (_missionType == MissionType.Fight)
            {
                if (!_fightComplete)
                {
                    _fightComplete = true;
                    _missionType = MissionType.Quest;
                    _currentObjective = GetRandomZone(MissionType.Quest);
                    _bot.GoToPoint(_currentObjective, false);
                }
            }
            else if (_missionType == MissionType.Quest)
            {
                TryExtract();
            }

            _waitForGroup = !IsGroupAligned();
        }

        private void TryExtract()
        {
            ExfiltrationPoint? closest = null;
            float minDist = float.MaxValue;

            foreach (var point in GameObject.FindObjectsOfType<ExfiltrationPoint>())
            {
                if (point.Status != EExfiltrationStatus.RegularMode)
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
                _bot.GoToPoint(closest.transform.position, false);
                Say(EPhraseTrigger.ExitLocated);
            }
            else
            {
                Say(EPhraseTrigger.MumblePhrase);
            }
        }

        private void OnObjectiveReached()
        {
            if (_missionType == MissionType.Loot && !_isLooting)
            {
                _isLooting = true;
                Say(EPhraseTrigger.OnLoot);
                _bot.Mover?.Stop();
                Invoke(nameof(ResumeMovement), 4f);
            }

            if (_readyToExtract && _missionType == MissionType.Quest)
            {
                Say(EPhraseTrigger.MumblePhrase);
                _bot?.Deactivate();
                _bot?.GetPlayer?.gameObject.SetActive(false);
            }
        }

        private void ResumeMovement()
        {
            if (_bot != null)
                _bot.GoToPoint(_currentObjective, false);
        }

        private Vector3 GetBestLootZone()
        {
            float highestValue = 0f;
            Vector3 bestPos = _bot?.Position ?? Vector3.zero;

            foreach (var container in _lootContainers)
            {
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
            float total = 0f;
            if (container?.ItemOwner?.RootItem == null)
                return 0f;

            foreach (var item in container.ItemOwner.RootItem.GetAllItems())
            {
                if (item?.Template?.Weight > 0)
                    total += item.Template.CreditsPrice;
            }

            return total;
        }

        private Vector3 GetRandomZone(MissionType type)
        {
            List<Vector3> list = type switch
            {
                MissionType.Fight => _hotZones,
                MissionType.Quest => _questZones,
                _ => new List<Vector3>()
            };

            if (list.Count == 0)
                return _bot?.Position ?? Vector3.zero;

            return list[_rng.Next(0, list.Count)];
        }

        private bool IsGroupAligned()
        {
            if (_group == null)
                return true;

            var teammates = _group.GetTeammates();
            if (teammates.Count == 0)
                return true;

            int nearby = 0;
            foreach (var mate in teammates)
            {
                if (mate != null && Vector3.Distance(mate.Position, _bot.Position) < SQUAD_COHESION_RANGE)
                    nearby++;
            }

            return nearby >= Mathf.CeilToInt(teammates.Count * 0.6f);
        }

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
    }

    public static class InventoryUtil
    {
        public static bool IsBackpackFull(BotOwner bot)
        {
            var inv = bot?.GetPlayer?.Inventory;
            var bpSlot = inv?.Equipment?.GetSlot(EquipmentSlot.Backpack);
            var backpack = bpSlot?.ContainedItem;

            if (backpack == null)
                return false;

            int count = 0;
            foreach (var item in backpack.GetAllItems())
            {
                count++;
                if (count >= 40)
                    return true;
            }

            return false;
        }
    }
}
