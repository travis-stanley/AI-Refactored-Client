#nullable enable

using UnityEngine;
using EFT;
using Comfort.Common;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Groups;
using System.Collections.Generic;

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

        private float _lastHitTime = -999f;
        private float _switchCooldown = 0f;
        private float _lastEchoTime = -999f;

        private const float InvestigateScanRadius = 4f;
        private const float InvestigateCooldown = 10f;
        private const float EchoCooldown = 5f;
        private const float EchoChance = 0.3f;

        private readonly List<float> _soundTimestamps = new(4);

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

        private void Update()
        {
            if (_bot == null || _cache == null || _profile == null || _bot.IsDead)
                return;

            if (_bot.GetPlayer != null && !_bot.GetPlayer.IsAI)
                return;

            float time = Time.time;

            if (_bot.Memory?.GoalEnemy != null)
            {
                _state = CombatState.Attack;
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
                    _state = CombatState.Patrol;
                    _fallbackPosition = null;
                }
                else
                {
                    var adjusted = _squadCoordinator?.ApplyOffsetTo(_fallbackPosition.Value) ?? _fallbackPosition.Value;
                    BotMovementHelper.SmoothMoveTo(_bot, adjusted);
                }
                return;
            }

            if (_suppress?.IsSuppressed() == true && _state != CombatState.Fallback)
            {
                TriggerSuppressedFallback();
                return;
            }

            if (_state == CombatState.Investigate && time - _lastHitTime > InvestigateCooldown)
            {
                _state = CombatState.Patrol;
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
                var adjusted = _squadCoordinator?.ApplyOffsetTo(_lastKnownEnemyPos.Value) ?? _lastKnownEnemyPos.Value;
                BotMovementHelper.SmoothMoveTo(_bot, adjusted);
                if (Vector3.Distance(_bot.Position, _lastKnownEnemyPos.Value) < 3f)
                    _lastKnownEnemyPos = null;
                return;
            }

            if (_cache.LastHeardTime + 4f > time && _state == CombatState.Patrol)
            {
                _soundTimestamps.Add(time);
                CleanupOldSoundEvents(time);

                if (_soundTimestamps.Count >= 2 && _profile.Caution > 0.5f)
                {
                    _state = CombatState.Investigate;
                    _lastHitTime = time;
                    _bot.BotTalk?.TrySay(EPhraseTrigger.NeedHelp);
                    EchoInvestigateToSquad();
                    return;
                }
            }

            if (_state == CombatState.Patrol && time >= _switchCooldown)
            {
                Vector3 target = HotspotSystem.GetRandomHotspot(_bot);
                var adjusted = _squadCoordinator?.ApplyOffsetTo(target) ?? target;
                BotMovementHelper.SmoothMoveTo(_bot, adjusted);
                _switchCooldown = time + Random.Range(15f, 45f);

                if (Random.value < 0.25f)
                    _bot.BotTalk?.TrySay(EPhraseTrigger.GoForward);
            }

            if (_state == CombatState.Investigate && !_lastKnownEnemyPos.HasValue)
            {
                Vector3 jitter = Random.insideUnitSphere * InvestigateScanRadius;
                Vector3 scan = _bot.Position + new Vector3(jitter.x, 0f, jitter.z);

                if (_tacticalMemory != null && !_tacticalMemory.WasRecentlyCleared(scan))
                {
                    var adjusted = _squadCoordinator?.ApplyOffsetTo(scan) ?? scan;
                    BotMovementHelper.SmoothMoveTo(_bot, adjusted);
                    _tacticalMemory.MarkCleared(scan);

                    if (Random.value < 0.3f)
                        _bot.BotTalk?.TrySay(EPhraseTrigger.Look);
                }
            }
        }

        private void TriggerSuppressedFallback()
        {
            if (_bot == null || _cache == null) return;

            _state = CombatState.Fallback;
            _fallbackPosition = null;

            _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
            float cohesion = Mathf.Lerp(0.6f, 1.2f, _profile?.Cohesion ?? 1f);

            if (_cache.PathCache != null)
            {
                Vector3 threatDir = _bot.LookDirection.normalized;
                List<Vector3> path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, threatDir, _cache.PathCache);
                if (path.Count > 0)
                {
                    Vector3 finalPoint = path[path.Count - 1];
                    _fallbackPosition = finalPoint;
                    var adjusted = _squadCoordinator?.ApplyOffsetTo(finalPoint) ?? finalPoint;
                    BotMovementHelper.SmoothMoveTo(_bot, adjusted, false, cohesion);
                    return;
                }
            }

            _fallbackPosition = _bot.Position - _bot.LookDirection.normalized * 5f;
            var fallback = _squadCoordinator?.ApplyOffsetTo(_fallbackPosition.Value) ?? _fallbackPosition.Value;
            BotMovementHelper.SmoothMoveTo(_bot, fallback, false, cohesion);

            EchoFallbackToSquad();
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

                if (Vector3.Distance(teammate.Position, _bot.Position) < 30f && Random.value < EchoChance)
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

                if (Vector3.Distance(teammate.Position, _bot.Position) < 30f && Random.value < EchoChance)
                {
                    var stateMachine = teammate.GetComponent<CombatStateMachine>();
                    if (stateMachine != null)
                        stateMachine.TriggerFallback(teammate.Position - teammate.LookDirection.normalized * 5f);
                }
            }

            _lastEchoTime = Time.time;
        }

        private void CleanupOldSoundEvents(float now)
        {
            for (int i = _soundTimestamps.Count - 1; i >= 0; i--)
            {
                if (now - _soundTimestamps[i] > 5f)
                    _soundTimestamps.RemoveAt(i);
            }
        }

        public void NotifyDamaged()
        {
            _lastHitTime = Time.time;
            _state = CombatState.Investigate;
            _bot?.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
        }

        public void NotifyEchoInvestigate()
        {
            if (_state == CombatState.Patrol)
            {
                _state = CombatState.Investigate;
                _lastHitTime = Time.time;
                _bot?.BotTalk?.TrySay(EPhraseTrigger.Cooperation);
            }
        }

        public void TriggerFallback(Vector3 position)
        {
            _fallbackPosition = position;
            _state = CombatState.Fallback;
            _bot?.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);
        }

        public bool IsInCombatState()
        {
            return _state == CombatState.Attack || _state == CombatState.Fallback || _state == CombatState.Investigate;
        }
    }
}
