#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Group;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Optimization;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    public class CombatStateMachine : MonoBehaviour
    {
        private enum CombatState { Patrol, Investigate, Attack, Fallback }

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private AIRefactoredBotOwner? _owner;
        private BotPersonalityProfile? _profile;
        private BotSuppressionReactionComponent? _suppress;
        private BotTacticalMemory? _tacticalMemory;
        private SquadPathCoordinator? _squadCoordinator;

        private CombatState _state = CombatState.Patrol;
        private Vector3? _fallbackPosition;
        private Vector3? _lastKnownEnemyPos;
        private Vector3? _lastMoveTarget;

        private float _lastHitTime = -999f;
        private float _lastStateChangeTime = -999f;
        private float _switchCooldown = 0f;
        private float _lastEchoTime = -999f;

        private const float InvestigateScanRadius = 4f;
        private const float InvestigateCooldown = 10f;
        private const float EchoCooldown = 5f;
        private const float EchoChance = 0.3f;
        private const float MinStateDuration = 2.5f;
        private const float MoveTargetTolerance = 1.0f;

        private readonly List<float> _soundTimestamps = new List<float>(4);

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _owner = GetComponent<AIRefactoredBotOwner>();
            _profile = _owner?.PersonalityProfile;
            _suppress = GetComponent<BotSuppressionReactionComponent>();
            _tacticalMemory = GetComponent<BotTacticalMemory>();
            _squadCoordinator = GetComponent<SquadPathCoordinator>();
        }

        public void Tick(float time)
        {
            if (_bot == null || _cache == null || _profile == null || _bot.IsDead || _bot.GetPlayer?.IsAI != true)
                return;

            if (_bot.Memory?.GoalEnemy != null)
            {
                ForceState(CombatState.Attack, time);
                _lastKnownEnemyPos = _bot.Memory.GoalEnemy.CurrPosition;
                _tacticalMemory?.RecordEnemyPosition(_lastKnownEnemyPos.Value);
                _bot.Sprint(true);
                return;
            }

            if (_state == CombatState.Fallback && _fallbackPosition.HasValue)
            {
                float dist = Vector3.Distance(_bot.Position, _fallbackPosition.Value);
                if (dist < 2f)
                {
                    ForceState(CombatState.Patrol, time);
                    _fallbackPosition = null;
                }
                else
                {
                    MoveSmoothlyTo(_squadCoordinator?.ApplyOffsetTo(_fallbackPosition.Value) ?? _fallbackPosition.Value);
                }
                return;
            }

            if (_suppress?.IsSuppressed() == true && _state != CombatState.Fallback && TimeSinceStateChange(time) >= MinStateDuration)
            {
                TriggerSuppressedFallback(time);
                return;
            }

            if (_state == CombatState.Investigate && time - _lastHitTime > InvestigateCooldown)
            {
                ForceState(CombatState.Patrol, time);
                _lastKnownEnemyPos = null;
            }

            if (_state == CombatState.Investigate && !_lastKnownEnemyPos.HasValue)
            {
                var lastEnemy = _tacticalMemory?.GetLastKnownEnemyPosition();
                if (lastEnemy.HasValue)
                    _lastKnownEnemyPos = lastEnemy;
            }

            if (_state == CombatState.Investigate && _lastKnownEnemyPos.HasValue)
            {
                MoveSmoothlyTo(_squadCoordinator?.ApplyOffsetTo(_lastKnownEnemyPos.Value) ?? _lastKnownEnemyPos.Value);

                if (Vector3.Distance(_bot.Position, _lastKnownEnemyPos.Value) < 3f)
                    _lastKnownEnemyPos = null;

                return;
            }

            if (_cache.LastHeardTime + 4f > time && _state == CombatState.Patrol)
            {
                _soundTimestamps.Add(time);
                CleanupOldSoundEvents(time);

                if (_soundTimestamps.Count >= 2 && _profile.Caution > 0.5f && TimeSinceStateChange(time) >= MinStateDuration)
                {
                    ForceState(CombatState.Investigate, time);
                    _lastHitTime = time;
                    _bot.BotTalk?.TrySay(EPhraseTrigger.NeedHelp);
                    EchoInvestigateToSquad();
                    return;
                }
            }

            if (_state == CombatState.Patrol && time >= _switchCooldown)
            {
                Vector3 target = HotspotSystem.GetRandomHotspot(_bot);
                MoveSmoothlyTo(_squadCoordinator?.ApplyOffsetTo(target) ?? target);

                _switchCooldown = time + UnityEngine.Random.Range(15f, 45f);

                if (UnityEngine.Random.value < 0.25f)
                    _bot.BotTalk?.TrySay(EPhraseTrigger.GoForward);
            }

            if (_state == CombatState.Investigate && !_lastKnownEnemyPos.HasValue)
            {
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * InvestigateScanRadius;
                Vector3 scan = _bot.Position + new Vector3(jitter.x, 0f, jitter.z);

                if (_tacticalMemory != null && !_tacticalMemory.WasRecentlyCleared(scan))
                {
                    MoveSmoothlyTo(_squadCoordinator?.ApplyOffsetTo(scan) ?? scan);
                    _tacticalMemory.MarkCleared(scan);

                    if (UnityEngine.Random.value < 0.3f)
                        _bot.BotTalk?.TrySay(EPhraseTrigger.Look);
                }
            }
        }

        private void TriggerSuppressedFallback(float time)
        {
            _bot?.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);

            Vector3 threatDir = _bot!.LookDirection.normalized;
            Vector3? fallback = null;

            if (_cache?.PathCache != null)
            {
                List<Vector3> path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, threatDir, _cache.PathCache);
                if (path.Count > 0)
                    fallback = path[path.Count - 1];
            }

            fallback ??= _bot.Position - threatDir * 5f;
            _fallbackPosition = fallback;

            Vector3 moveTo = _squadCoordinator?.ApplyOffsetTo(fallback.Value) ?? fallback.Value;
            MoveSmoothlyTo(moveTo);


            BotTeamLogic.BroadcastFallback(_bot, fallback.Value);

            ForceState(CombatState.Fallback, time);
            EchoFallbackToSquad();
        }

        private void MoveSmoothlyTo(Vector3 destination)
        {
            if (!_lastMoveTarget.HasValue || Vector3.Distance(_lastMoveTarget.Value, destination) > MoveTargetTolerance)
            {
                BotMovementHelper.SmoothMoveTo(_bot!, destination, false, _profile?.Cohesion ?? 1f);
                _lastMoveTarget = destination;
            }
        }

        private void ForceState(CombatState newState, float time)
        {
            if (_state != newState)
            {
                _state = newState;
                _lastStateChangeTime = time;
                _lastMoveTarget = null;
            }
        }

        private float TimeSinceStateChange(float now) => now - _lastStateChangeTime;

        private void CleanupOldSoundEvents(float now)
        {
            for (int i = _soundTimestamps.Count - 1; i >= 0; i--)
            {
                if (now - _soundTimestamps[i] > 5f)
                    _soundTimestamps.RemoveAt(i);
            }
        }

        private void EchoInvestigateToSquad()
        {
            if (_bot?.BotsGroup == null || Time.time - _lastEchoTime < EchoCooldown)
                return;

            var group = _bot.BotsGroup;
            for (int i = 0; i < group.MembersCount; i++)
            {
                var teammate = group.Member(i);
                if (teammate == null || teammate == _bot)
                    continue;

                if (Vector3.Distance(teammate.Position, _bot.Position) < 30f && UnityEngine.Random.value < EchoChance)
                {
                    var stateMachine = teammate.GetComponent<CombatStateMachine>();
                    stateMachine?.NotifyEchoInvestigate();
                }
            }

            _lastEchoTime = Time.time;
        }

        private void EchoFallbackToSquad()
        {
            if (_bot?.BotsGroup == null || Time.time - _lastEchoTime < EchoCooldown)
                return;

            var group = _bot.BotsGroup;
            for (int i = 0; i < group.MembersCount; i++)
            {
                var teammate = group.Member(i);
                if (teammate == null || teammate == _bot)
                    continue;

                if (Vector3.Distance(teammate.Position, _bot.Position) < 30f && UnityEngine.Random.value < EchoChance)
                {
                    var stateMachine = teammate.GetComponent<CombatStateMachine>();
                    stateMachine?.TriggerFallback(teammate.Position - teammate.LookDirection.normalized * 5f);
                }
            }

            _lastEchoTime = Time.time;
        }

        public void NotifyDamaged()
        {
            _lastHitTime = Time.time;
            ForceState(CombatState.Investigate, _lastHitTime);
            _bot?.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
        }

        public void NotifyEchoInvestigate()
        {
            if (_state == CombatState.Patrol)
            {
                float now = Time.time;
                ForceState(CombatState.Investigate, now);
                _lastHitTime = now;
                _bot?.BotTalk?.TrySay(EPhraseTrigger.Cooperation);
            }
        }

        public void TriggerFallback(Vector3 position)
        {
            _fallbackPosition = position;
            ForceState(CombatState.Fallback, Time.time);
            _bot?.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);
        }

        public bool IsInCombatState()
        {
            return _state == CombatState.Attack || _state == CombatState.Fallback || _state == CombatState.Investigate;
        }
    }
}
