#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Looting;
using AIRefactored.Core;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using EFT.Interactive;
using EFT.InventoryLogic;
using System;
using System.Collections.Generic;
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
        private Queue<Vector3> _questRoute = new();
        private float _lastCombatTime;
        private bool _inCombatPause;

        private float _lastMoveTime;
        private float _stuckSince;
        private Vector3 _lastPos;

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        private const float GlobalLootThreshold = 0.85f;
        private const int CombatSwitchThreshold = 2;
        private const int LootItemCountThreshold = 40;

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

            _lastPos = _bot.Position;
            _lastMoveTime = Time.time;

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

            if (_bot.GetPlayer.IsYourPlayer)
                return;

            bool inCombat = _combat?.IsInCombatState() == true;
            if (inCombat)
            {
                _lastCombatTime = time;
                if (_missionType == MissionType.Quest)
                    _inCombatPause = true;
            }

            if (_missionType == MissionType.Quest && _inCombatPause && time - _lastCombatTime > 5f)
            {
                _inCombatPause = false;
                _log.LogInfo($"[BotMissionSystem] {BotName()} exiting combat pause → resuming questing.");
                ResumeQuesting();
            }

            float moved = Vector3.Distance(_bot.Position, _lastPos);
            if (moved > 0.3f)
            {
                _lastPos = _bot.Position;
                _lastMoveTime = time;
            }
            else if (time - _lastMoveTime > 25f && time - _stuckSince > 30f)
            {
                _stuckSince = time;
                Vector3 fallback = _bot.Position + UnityEngine.Random.insideUnitSphere * 8f;
                fallback.y = _bot.Position.y;
                _log.LogInfo($"[BotMissionSystem] {BotName()} detected as stuck, issuing fallback move → {fallback}");
                BotMovementHelper.SmoothMoveTo(_bot, fallback);
            }

            if (_missionType != MissionType.Fight && _bot.EnemiesController?.EnemyInfos.Count >= CombatSwitchThreshold)
            {
                _log.LogInfo($"[BotMissionSystem] {BotName()} detected high combat → switching to Fight.");
                _missionType = MissionType.Fight;
                _currentObjective = GetStaggeredMissionObjective(GetRandomZone(MissionType.Fight));
                BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                return;
            }

            if (_missionType == MissionType.Quest || _missionType == MissionType.Loot)
            {
                if (ShouldExtractEarly(_bot))
                {
                    _log.LogInfo($"[BotMissionSystem] {BotName()} loot value reached → extracting early.");
                    TryExtract();
                    return;
                }
            }

            if (_waitForGroup && !IsGroupAligned() && time - _lastUpdate < 20f)
                return;

            float missionCooldown = _baseCooldown + UnityEngine.Random.Range(0f, (_personality?.ChaosFactor ?? 0f) * 8f);
            if (time - _lastUpdate > missionCooldown)
            {
                EvaluateMission();
                _lastUpdate = time;
            }

            if (!_inCombatPause && Vector3.Distance(_bot.Position, _currentObjective) < REACHED_THRESHOLD)
            {
                OnObjectiveReached();
            }
        }

        private bool ShouldExtractEarly(BotOwner bot)
        {
            float retreatThreshold = _personality?.RetreatThreshold ?? GlobalLootThreshold;

            var inv = bot.GetPlayer?.Inventory;
            var slot = inv?.Equipment?.GetSlot(EquipmentSlot.Backpack);
            var backpack = slot?.ContainedItem;

            if (backpack == null)
                return false;

            int itemCount = 0;
            foreach (var item in backpack.GetAllItems())
            {
                if (item != null)
                    itemCount++;
            }

            float fullness = (float)itemCount / LootItemCountThreshold;
            bool cautious = _personality?.Caution > 0.6f;
            bool notFrenzied = !_personality?.IsFrenzied ?? true;

            return fullness >= retreatThreshold && cautious && notFrenzied;
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

            Vector3 rawObjective = _missionType switch
            {
                MissionType.Quest => GetNextQuestObjective(),
                _ => GetInitialObjective(_missionType)
            };

            _currentObjective = GetStaggeredMissionObjective(rawObjective);

            if (Vector3.Distance(_bot.Position, _currentObjective) < 2f)
            {
                _log.LogWarning($"[BotMissionSystem] {BotName()} picked too-close objective → retrying.");
                _currentObjective = _bot.Position + UnityEngine.Random.insideUnitSphere * 10f;
                _currentObjective.y = _bot.Position.y;
            }

            _log.LogInfo($"[BotMissionSystem] {BotName()} mission: {_missionType}, target: {_currentObjective}");
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

        private void PopulateQuestRoute()
        {
            _questRoute.Clear();
            var set = HotspotLoader.GetHotspotsForCurrentMap(_bot?.Profile.Info.Settings.Role ?? WildSpawnType.assault);
            if (set == null || set.Points.Count == 0)
                return;

            var used = new HashSet<int>();
            int count = UnityEngine.Random.Range(2, 4);
            while (_questRoute.Count < count && used.Count < set.Points.Count)
            {
                int i = UnityEngine.Random.Range(0, set.Points.Count);
                if (used.Add(i))
                    _questRoute.Enqueue(set.Points[i]);
            }
        }

        private Vector3 GetNextQuestObjective()
        {
            if (_questRoute.Count == 0)
                PopulateQuestRoute();

            if (_questRoute.Count > 0)
                return _questRoute.Dequeue();

            return _bot?.Position ?? Vector3.zero;
        }

        private void ResumeQuesting()
        {
            if (_questRoute.Count == 0)
                PopulateQuestRoute();

            if (_questRoute.Count > 0)
            {
                _currentObjective = GetStaggeredMissionObjective(GetNextQuestObjective());
                BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
            }
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
                        PopulateQuestRoute();
                        _currentObjective = GetStaggeredMissionObjective(GetNextQuestObjective());
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

            if (_missionType == MissionType.Quest)
            {
                if (_questRoute.Count > 0)
                {
                    _currentObjective = GetStaggeredMissionObjective(GetNextQuestObjective());
                    _log.LogInfo($"[BotMissionSystem] {BotName()} advancing to next quest hotspot → {_currentObjective}");
                    BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                }
                else if (_readyToExtract || (_lootComplete && _fightComplete))
                {
                    _log.LogInfo($"[BotMissionSystem] {BotName()} completed all objectives → extracting.");
                    Say(EPhraseTrigger.ExitLocated);
                    _bot?.Deactivate();
                    _bot?.GetPlayer?.gameObject.SetActive(false);
                }
                else
                {
                    _log.LogInfo($"[BotMissionSystem] {BotName()} quest path empty → refilling.");
                    ResumeQuesting();
                }
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
                if (count >= LootItemCountThreshold)
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
                _log.LogWarning($"[AIRefactored-Mission] Voice failed: {ex.Message}");
            }
        }

        private string BotName()
        {
            return _bot?.Profile?.Info?.Nickname ?? "UnknownBot";
        }
    }
}
