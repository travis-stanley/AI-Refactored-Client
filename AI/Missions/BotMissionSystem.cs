#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Group;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Looting;
using AIRefactored.Core;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using System;
using UnityEngine;

namespace AIRefactored.AI.Missions
{
    public class BotMissionSystem
    {
        public enum MissionType { Loot, Fight, Quest }

        private BotOwner? _bot;
        private BotGroupSyncCoordinator? _group;
        private CombatStateMachine? _combat;
        private BotPersonalityProfile? _personality;
        private BotLootScanner? _lootScanner;
        private BotDeadBodyScanner? _deadBodyScanner;

        private Vector3 _currentObjective;
        private MissionType _missionType;

        private float _lastUpdate;
        private const float _baseCooldown = 15f;
        private const float REACHED_THRESHOLD = 6f;
        private const float SQUAD_COHESION_RANGE = 10f;

        private bool _readyToExtract = false;
        private bool _waitForGroup = false;
        private bool _forcedMission = false;
        private bool _lootComplete = false;
        private bool _fightComplete = false;

        private readonly System.Random _rng = new();

        public void Initialize(BotOwner bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            if (_bot.GetPlayer?.IsYourPlayer == true)
                return;

            _group = BotCacheUtility.GetCache(bot.GetPlayer!)?.GroupBehavior != null
                ? bot.GetPlayer!.GetComponent<BotGroupSyncCoordinator>()
                : null;

            _combat = BotCacheUtility.GetCache(bot.GetPlayer!)?.Combat;
            _personality = BotRegistry.Get(_bot.Profile.Id);
            _lootScanner = BotCacheUtility.GetCache(bot.GetPlayer!)?.AIRefactoredBotOwner?.GetComponent<BotLootScanner>();
            _deadBodyScanner = BotCacheUtility.GetCache(bot.GetPlayer!)?.AIRefactoredBotOwner?.GetComponent<BotDeadBodyScanner>();

            if (!_forcedMission)
                PickMission();
        }

        public void SetForcedMission(MissionType mission)
        {
            _missionType = mission;
            _forcedMission = true;
        }

        public void Tick(float time)
        {
            if (_bot == null || _bot.GetPlayer == null || !_bot.GetPlayer.HealthController.IsAlive)
                return;

            if (_bot.GetPlayer.IsYourPlayer || (_combat?.IsInCombatState() == true))
                return;

            if (_waitForGroup && !IsGroupAligned())
                return;

            float missionCooldown = _baseCooldown + UnityEngine.Random.Range(0f, (_personality?.ChaosFactor ?? 0f) * 8f);
            if (time - _lastUpdate > missionCooldown)
            {
                EvaluateMission();
                _lastUpdate = time;
            }

            if (Vector3.Distance(_bot.Position, _currentObjective) < REACHED_THRESHOLD)
            {
                OnObjectiveReached();
            }
        }

        private void PickMission()
        {
            if (_personality == null)
            {
                _missionType = MissionType.Loot;
                _currentObjective = GetStaggeredMissionObjective(GetLootObjective());
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

            Vector3 rawObjective = GetInitialObjective(_missionType);
            _currentObjective = GetStaggeredMissionObjective(rawObjective);
            BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
            BotTeamLogic.BroadcastMissionType(_bot, _missionType);
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
                MissionType.Loot => GetLootObjective(),
                MissionType.Fight => GetRandomZone(MissionType.Fight),
                MissionType.Quest => HotspotSystem.GetRandomHotspot(_bot!),
                _ => _bot?.Position ?? Vector3.zero
            };
        }

        private Vector3 GetStaggeredMissionObjective(Vector3 target)
        {
            if (_group == null || _bot == null)
                return target;

            int index = 0;
            var teammates = _group.GetTeammates();
            for (int i = 0; i < teammates.Count; i++)
            {
                if (teammates[i] == _bot)
                {
                    index = i;
                    break;
                }
            }

            return GetStaggeredPosition(target, index);
        }

        private Vector3 GetStaggeredPosition(Vector3 target, int index)
        {
            float angle = index * 137f;
            float radius = 1.5f + (index % 3) * 0.75f;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ) * radius;

            return target + offset;
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
                        _currentObjective = GetStaggeredMissionObjective(GetRandomZone(MissionType.Fight));
                        BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                        BotTeamLogic.BroadcastMissionType(_bot, _missionType);
                    }
                    else if (!_lootComplete)
                    {
                        _currentObjective = GetStaggeredMissionObjective(GetLootObjective());
                        BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                    }
                    break;

                case MissionType.Fight:
                    if (!_fightComplete)
                    {
                        _fightComplete = true;
                        _missionType = MissionType.Quest;
                        _currentObjective = GetStaggeredMissionObjective(HotspotSystem.GetRandomHotspot(_bot));
                        BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                        BotTeamLogic.BroadcastMissionType(_bot, _missionType);
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
            if (_missionType == MissionType.Loot && !_lootComplete)
            {
                _lootScanner?.TryLootNearby();
                _deadBodyScanner?.TryLootNearby();
                _lootComplete = true;
                Say(EPhraseTrigger.OnLoot);
                return;
            }

            if (_readyToExtract && _missionType == MissionType.Quest)
            {
                Say(EPhraseTrigger.MumblePhrase);
                _bot?.Deactivate();
                _bot?.GetPlayer?.gameObject.SetActive(false);
            }
        }

        private Vector3 GetLootObjective()
        {
            return _lootScanner?.GetHighestValueLootPoint() ?? _bot?.Position ?? Vector3.zero;
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

        private void Say(EPhraseTrigger phrase)
        {
            try
            {
                if (!FikaHeadlessDetector.IsHeadless)
                {
                    _bot?.GetPlayer?.Say(phrase);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIRefactored-Mission] Voice failed: {ex.Message}");
            }
        }
    }
}
