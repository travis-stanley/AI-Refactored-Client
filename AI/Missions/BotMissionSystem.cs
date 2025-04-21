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
    /// <summary>
    /// Drives high-level objective behavior for bots: Questing, Looting, Fighting, and Extraction.
    /// Dynamically switches mission types based on squad role, danger, value thresholds, and combat flow.
    /// </summary>
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
        private Queue<Vector3> _questRoute = new();

        private float _lastUpdate;
        private float _lastCombatTime;
        private float _lastMoveTime;
        private float _stuckSince;
        private Vector3 _lastPos;

        private MissionType _missionType;

        private bool _inCombatPause;
        private bool _waitForGroup;
        private bool _forcedMission;
        private bool _readyToExtract;
        private bool _lootComplete;
        private bool _fightComplete;

        private const float BaseCooldown = 10f;
        private const float ReachedThreshold = 6f;
        private const float StuckDuration = 25f;
        private const float StuckCooldown = 30f;

        private const int CombatSwitchThreshold = 2;
        private const int LootItemCountThreshold = 40;
        private const float SquadCohesionRange = 10f;
        private const float GlobalLootThreshold = 0.85f;

        private readonly System.Random _rng = new();

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        public void Initialize(BotOwner bot)
        {
            _bot = bot;
            if (_bot.GetPlayer?.IsYourPlayer == true)
                return;

            var player = bot.GetPlayer!;
            _group = BotCacheUtility.GetCache(player)?.GroupBehavior != null
                ? player.GetComponent<BotGroupSyncCoordinator>()
                : null;

            _combat = BotCacheUtility.GetCache(player)?.Combat;
            _personality = BotRegistry.Get(_bot.Profile.Id);
            _lootScanner = BotCacheUtility.GetCache(player)?.AIRefactoredBotOwner?.GetComponent<BotLootScanner>();
            _deadBodyScanner = BotCacheUtility.GetCache(player)?.AIRefactoredBotOwner?.GetComponent<BotDeadBodyScanner>();

            _lastPos = bot.Position;
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
            if (_bot?.GetPlayer?.HealthController == null || !_bot.GetPlayer.HealthController.IsAlive)
                return;

            if (_bot.GetPlayer.IsYourPlayer)
                return;

            float moved = Vector3.Distance(_bot.Position, _lastPos);
            if (moved > 0.3f)
            {
                _lastPos = _bot.Position;
                _lastMoveTime = time;
            }
            else if (time - _lastMoveTime > StuckDuration && time - _stuckSince > StuckCooldown)
            {
                _stuckSince = time;
                Vector3 fallback = _bot.Position + UnityEngine.Random.insideUnitSphere * 6f;
                fallback.y = _bot.Position.y;
                _log.LogInfo($"[BotMissionSystem] {BotName()} detected as stuck → moving to {fallback}");
                BotMovementHelper.SmoothMoveTo(_bot, fallback);
            }

            if (_missionType != MissionType.Fight && _bot.EnemiesController?.EnemyInfos.Count >= CombatSwitchThreshold)
            {
                _log.LogInfo($"[BotMissionSystem] {BotName()} switching to Fight due to high enemy presence.");
                _missionType = MissionType.Fight;
                _currentObjective = GetStaggeredMissionObjective(GetRandomZone(MissionType.Fight));
                BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                return;
            }

            if (_combat?.IsInCombatState() == true)
            {
                _lastCombatTime = time;
                if (_missionType == MissionType.Quest)
                    _inCombatPause = true;
            }

            if (_missionType == MissionType.Quest && _inCombatPause && time - _lastCombatTime > 4f)
            {
                _inCombatPause = false;
                _log.LogInfo($"[BotMissionSystem] {BotName()} resuming quest after combat pause.");
                ResumeQuesting();
            }

            if ((_missionType == MissionType.Quest || _missionType == MissionType.Loot) && ShouldExtractEarly())
            {
                _log.LogInfo($"[BotMissionSystem] {BotName()} ready to extract due to loot value.");
                TryExtract();
                return;
            }

            if (_waitForGroup && !IsGroupAligned() && time - _lastUpdate < 15f)
                return;

            float chaosDelay = UnityEngine.Random.Range(0f, (_personality?.ChaosFactor ?? 0f) * 8f);
            if (time - _lastUpdate > BaseCooldown + chaosDelay)
            {
                EvaluateMission();
                _lastUpdate = time;
            }

            if (!_inCombatPause && Vector3.Distance(_bot.Position, _currentObjective) < ReachedThreshold)
                OnObjectiveReached();
        }

        private void PickMission()
        {
            if (_bot == null) return;

            bool isPmc = _bot.Profile.Info.Side is EPlayerSide.Usec or EPlayerSide.Bear;
            _missionType = _personality?.PreferredMission switch
            {
                MissionBias.Quest => isPmc ? MissionType.Quest : MissionType.Loot,
                MissionBias.Fight => MissionType.Fight,
                MissionBias.Loot => MissionType.Loot,
                _ => RandomizeMissionType(isPmc)
            };

            Vector3 target = _missionType switch
            {
                MissionType.Quest => GetNextQuestObjective(),
                MissionType.Loot => GetLootObjective(),
                MissionType.Fight => GetRandomZone(MissionType.Fight),
                _ => _bot.Position
            };

            _currentObjective = GetStaggeredMissionObjective(target);
            if (Vector3.Distance(_bot.Position, _currentObjective) < 2f)
                _currentObjective = _bot.Position + UnityEngine.Random.insideUnitSphere * 6f;

            _log.LogInfo($"[BotMissionSystem] {BotName()} starting mission {_missionType} → {_currentObjective}");
            BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
            BotTeamLogic.BroadcastMissionType(_bot, _missionType);
        }

        private void EvaluateMission()
        {
            if (_bot?.GetPlayer?.IsYourPlayer == true)
                return;

            switch (_missionType)
            {
                case MissionType.Loot:
                    if (_bot != null)
                    {
                        if (!_readyToExtract && IsBackpackFull())
                        {
                            _readyToExtract = true;
                            Say(EPhraseTrigger.OnFight);

                            _missionType = MissionType.Fight;

                            Vector3 fightTarget = GetRandomZone(MissionType.Fight);
                            _currentObjective = GetStaggeredMissionObjective(fightTarget);

                            BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                            BotTeamLogic.BroadcastMissionType(_bot, _missionType);
                        }
                        else if (!_lootComplete)
                        {
                            Vector3 lootTarget = GetLootObjective();
                            _currentObjective = GetStaggeredMissionObjective(lootTarget);

                            if (_currentObjective != Vector3.zero)
                                BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                        }
                    }
                    break;

                case MissionType.Fight:
                    if (_bot != null && !_fightComplete)
                    {
                        _fightComplete = true;
                        _missionType = MissionType.Quest;

                        PopulateQuestRoute();

                        Vector3 questTarget = GetNextQuestObjective();
                        _currentObjective = GetStaggeredMissionObjective(questTarget);

                        if (_currentObjective != Vector3.zero)
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
            if (_bot == null)
                return;

            if (_missionType == MissionType.Loot && !_lootComplete)
            {
                _lootScanner?.TryLootNearby();
                _deadBodyScanner?.TryLootNearby();
                _lootComplete = true;
                Say(EPhraseTrigger.OnLoot);
            }

            if (_missionType == MissionType.Quest)
            {
                if (_questRoute.Count > 0)
                {
                    _currentObjective = GetStaggeredMissionObjective(GetNextQuestObjective());
                    _log.LogInfo($"[BotMissionSystem] {BotName()} next quest objective → {_currentObjective}");
                    BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
                }
                else if (_readyToExtract || (_lootComplete && _fightComplete))
                {
                    _log.LogInfo($"[BotMissionSystem] {BotName()} completed mission → extracting.");
                    Say(EPhraseTrigger.ExitLocated);
                    _bot.Deactivate();
                    _bot.GetPlayer?.gameObject.SetActive(false);
                }
                else
                {
                    ResumeQuesting();
                }
            }
        }

        private void ResumeQuesting()
        {
            if (_bot == null)
                return;

            if (_questRoute.Count == 0)
                PopulateQuestRoute();

            if (_questRoute.Count > 0)
            {
                _currentObjective = GetStaggeredMissionObjective(GetNextQuestObjective());
                BotMovementHelper.SmoothMoveTo(_bot, _currentObjective);
            }
        }

        private void PopulateQuestRoute()
        {
            _questRoute.Clear();
            var points = HotspotLoader.GetAllHotspotsRaw();
            if (points.Count == 0 || _bot == null)
                return;

            int count = UnityEngine.Random.Range(2, 4);
            var used = new HashSet<int>();

            while (_questRoute.Count < count && used.Count < points.Count)
            {
                int i = UnityEngine.Random.Range(0, points.Count);
                if (used.Add(i))
                    _questRoute.Enqueue(points[i]);
            }
        }

        private bool IsBackpackFull()
        {
            var backpack = _bot?.GetPlayer?.Inventory?.Equipment?.GetSlot(EquipmentSlot.Backpack)?.ContainedItem;
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

        private bool ShouldExtractEarly()
        {
            if (_bot == null) return false;

            float threshold = _personality?.RetreatThreshold ?? GlobalLootThreshold;
            var backpack = _bot.GetPlayer?.Inventory?.Equipment?.GetSlot(EquipmentSlot.Backpack)?.ContainedItem;
            if (backpack == null) return false;

            int count = 0;
            foreach (var item in backpack.GetAllItems())
            {
                if (item != null)
                    count++;
                if (count >= LootItemCountThreshold)
                    break;
            }

            float fullness = (float)count / LootItemCountThreshold;
            return fullness >= threshold && (_personality?.Caution ?? 0f) > 0.6f && !(_personality?.IsFrenzied ?? false);
        }

        private Vector3 GetLootObjective() =>
            _lootScanner?.GetHighestValueLootPoint() ?? _bot?.Position ?? Vector3.zero;

        private Vector3 GetNextQuestObjective()
        {
            if (_questRoute.Count == 0)
                PopulateQuestRoute();

            return _questRoute.Count > 0 ? _questRoute.Dequeue() : _bot?.Position ?? Vector3.zero;
        }

        private Vector3 GetRandomZone(MissionType type)
        {
            if (type != MissionType.Fight || _bot == null)
                return _bot?.Position ?? Vector3.zero;

            var zones = GameObject.FindObjectsOfType<BotZone>();
            if (zones.Length > 0)
                return zones[_rng.Next(0, zones.Length)].transform.position;

            return _bot.Position;
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

            return target + new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad)
            ) * radius;
        }

        private bool IsGroupAligned()
        {
            if (_group == null || _bot == null)
                return true;

            var mates = _group.GetTeammates();
            int near = 0;

            foreach (var m in mates)
            {
                if (m != null && Vector3.Distance(m.Position, _bot.Position) < SquadCohesionRange)
                    near++;
            }

            return near >= Mathf.CeilToInt(mates.Count * 0.6f);
        }

        private void TryExtract()
        {
            if (_bot == null || _bot.IsDead)
                return;

            var player = _bot.GetPlayer;
            if (player == null || player.IsYourPlayer)
                return;

            try
            {
                ExfiltrationPoint? closest = null;
                float minDist = float.MaxValue;
                Vector3 botPos = _bot.Position;

                var exfils = GameObject.FindObjectsOfType<ExfiltrationPoint>();
                if (exfils == null || exfils.Length == 0)
                    return;

                for (int i = 0; i < exfils.Length; i++)
                {
                    var point = exfils[i];
                    if (point == null || point.Status != EExfiltrationStatus.RegularMode)
                        continue;

                    float dist = Vector3.Distance(botPos, point.transform.position);
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
            }
            catch (System.Exception ex)
            {
                _log.LogWarning($"[BotMissionSystem] ❌ Extraction failed: {ex.Message}");
            }
        }

        private void Say(EPhraseTrigger phrase)
        {
            try
            {
                if (!FikaHeadlessDetector.IsHeadless)
                    _bot?.GetPlayer?.Say(phrase);
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[AIRefactored-Mission] Voice failed: {ex.Message}");
            }
        }

        private MissionType RandomizeMissionType(bool isPmc)
        {
            int roll = _rng.Next(0, 100);
            if (roll < 30) return MissionType.Loot;
            if (roll < 65) return MissionType.Fight;
            return isPmc ? MissionType.Quest : MissionType.Loot;
        }

        private string BotName() =>
            _bot?.Profile?.Info?.Nickname ?? "UnknownBot";
    }
}
