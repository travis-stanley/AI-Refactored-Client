#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Optimization;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Controls high-level combat behavior for a bot using a finite state machine.
    /// Manages transitions between patrol, investigate, engage, attack, and fallback.
    /// Supports squad coordination and suppression recovery.
    /// </summary>
    public class CombatStateMachine
    {
        #region Enums

        private enum CombatState { Patrol, Investigate, Engage, Attack, Fallback }

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private AIRefactoredBotOwner? _owner;
        private BotPersonalityProfile? _profile;
        private BotSuppressionReactionComponent? _suppress;
        private BotTacticalMemory? _tacticalMemory;
        private SquadPathCoordinator? _squadCoordinator;
        private BotGroupSyncCoordinator? _groupSync;

        private CombatState _state = CombatState.Patrol;

        private Vector3? _fallbackPosition;
        private Vector3? _lastKnownEnemyPos;
        private Vector3? _lastMoveTarget;

        private float _lastHitTime = -999f;
        private float _lastStateChangeTime = -999f;
        private float _switchCooldown = 0f;
        private float _lastEchoTime = -999f;
        private float _lastMoveCommandTime = -999f;

        private const float InvestigateScanRadius = 4f;
        private const float InvestigateCooldown = 8f;
        private const float EchoCooldown = 4f;
        private const float EchoChance = 0.5f;
        private const float MinStateDuration = 1.25f;
        private const float MoveTargetTolerance = 1.0f;
        private const float RepathIfStuckDuration = 5f;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
            _owner = cache.AIRefactoredBotOwner;
            _profile = _owner?.PersonalityProfile;
            _suppress = cache.Suppression;
            _tacticalMemory = cache.TacticalMemory;
            _squadCoordinator = cache.SquadPath;
            _groupSync = _cache.GroupBehavior?.GroupSync;
        }

        #endregion

        #region Tick

        public void Tick(float time)
        {
            if (_bot == null || _cache == null || _profile == null || _bot.IsDead || _bot.GetPlayer?.IsAI != true)
                return;

            if (_state == CombatState.Fallback && _fallbackPosition.HasValue)
            {
                HandleFallback(time);
                return;
            }

            if (_suppress?.IsSuppressed() == true && _state != CombatState.Fallback && TimeSinceStateChange(time) >= MinStateDuration)
            {
                TriggerSuppressedFallback(time);
                return;
            }

            IPlayer? enemy = _cache.ThreatSelector.CurrentTarget ?? _bot.Memory?.GoalEnemy?.Person;
            if (enemy != null && enemy.HealthController.IsAlive)
            {
                HandleEnemySpotted(enemy, time);
                return;
            }

            switch (_state)
            {
                case CombatState.Engage:
                    HandleEngage(time);
                    break;
                case CombatState.Investigate:
                    HandleInvestigate(time);
                    break;
                case CombatState.Patrol:
                    HandlePatrol(time);
                    break;
            }

            TryRepathIfStuck(time);
        }

        #endregion

        #region State Handlers

        private void HandleFallback(float time)
        {
            if (_bot == null || !_fallbackPosition.HasValue)
                return;

            float dist = Vector3.Distance(_bot.Position, _fallbackPosition.Value);
            if (dist < 2f)
            {
                _fallbackPosition = null;
                ForceState(_lastKnownEnemyPos.HasValue ? CombatState.Engage : CombatState.Patrol, time);
                return;
            }

            Vector3 target = _squadCoordinator?.ApplyOffsetTo(_fallbackPosition.Value) ?? _fallbackPosition.Value;
            MoveSmoothlyTo(target);
            TrySetStanceFromNearbyCover(target);
        }

        private void HandleEnemySpotted(IPlayer enemy, float time)
        {
            if (_bot == null) return;

            Vector3 pos = enemy.Transform.position;
            _lastKnownEnemyPos = pos;
            _tacticalMemory?.RecordEnemyPosition(pos);

            if (_state != CombatState.Engage && _state != CombatState.Attack)
            {
                ForceState(CombatState.Engage, time);
                _bot.Sprint(true);
                EchoSpottedEnemyToSquad(pos);
            }
        }

        private void HandleEngage(float time)
        {
            if (_bot == null || _profile == null || !_lastKnownEnemyPos.HasValue)
                return;

            float dist = Vector3.Distance(_bot.Position, _lastKnownEnemyPos.Value);
            if (dist < _profile.EngagementRange)
            {
                ForceState(CombatState.Attack, time);
            }
            else
            {
                Vector3 target = _squadCoordinator?.ApplyOffsetTo(_lastKnownEnemyPos.Value) ?? _lastKnownEnemyPos.Value;
                MoveSmoothlyTo(target);
                TrySetStanceFromNearbyCover(target);
            }
        }

        private void HandleInvestigate(float time)
        {
            if (_bot == null || _tacticalMemory == null)
                return;

            if (time - _lastHitTime > InvestigateCooldown)
            {
                ForceState(CombatState.Patrol, time);
                _lastKnownEnemyPos = null;
                return;
            }

            if (_lastKnownEnemyPos.HasValue)
            {
                Vector3 target = _squadCoordinator?.ApplyOffsetTo(_lastKnownEnemyPos.Value) ?? _lastKnownEnemyPos.Value;
                MoveSmoothlyTo(target);
                if (Vector3.Distance(_bot.Position, _lastKnownEnemyPos.Value) < 3f)
                    _lastKnownEnemyPos = null;
            }
            else if (_tacticalMemory.GetRecentEnemyMemory() is Vector3 mem)
            {
                _lastKnownEnemyPos = mem;
                Vector3 target = _squadCoordinator?.ApplyOffsetTo(mem) ?? mem;
                MoveSmoothlyTo(target);
                TrySetStanceFromNearbyCover(target);
            }
            else
            {
                Vector3 jitter = UnityEngine.Random.insideUnitSphere * InvestigateScanRadius;
                Vector3 scan = _bot.Position + new Vector3(jitter.x, 0f, jitter.z);
                if (!_tacticalMemory.WasRecentlyCleared(scan))
                {
                    Vector3 target = _squadCoordinator?.ApplyOffsetTo(scan) ?? scan;
                    MoveSmoothlyTo(target);
                    _tacticalMemory.MarkCleared(scan);
                    if (UnityEngine.Random.value < 0.2f)
                        _bot.BotTalk?.TrySay(EPhraseTrigger.Look);
                }
            }
        }

        private void HandlePatrol(float time)
        {
            if (_bot == null || _cache == null || _profile == null)
                return;

            if (_cache.LastHeardTime + 3f > time && _profile.Caution > 0.35f && TimeSinceStateChange(time) > MinStateDuration)
            {
                ForceState(CombatState.Investigate, time);
                _lastHitTime = time;
                _bot.BotTalk?.TrySay(EPhraseTrigger.NeedHelp);
                EchoInvestigateToSquad();
                return;
            }

            if (_cache.InjurySystem?.ShouldHeal() == true)
            {
                _bot.BotTalk?.TrySay(EPhraseTrigger.NeedHelp);
                ForceState(CombatState.Fallback, time);
                return;
            }

            if (time >= _switchCooldown)
            {
                Vector3 target = HotspotSystem.GetRandomHotspot(_bot);
                MoveSmoothlyTo(_squadCoordinator?.ApplyOffsetTo(target) ?? target);
                _switchCooldown = time + UnityEngine.Random.Range(12f, 30f);
                if (UnityEngine.Random.value < 0.25f)
                    _bot.BotTalk?.TrySay(EPhraseTrigger.GoForward);
            }
        }

        #endregion

        #region Suppression Fallback

        private void TriggerSuppressedFallback(float time)
        {
            if (_bot == null) return;

            _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);

            if (_cache?.PathCache != null)
            {
                Vector3 dir = _bot.LookDirection.normalized;
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, dir, _cache.PathCache);
                if (path.Count > 0)
                {
                    _fallbackPosition = path[path.Count - 1];
                    Vector3 target = _squadCoordinator?.ApplyOffsetTo(_fallbackPosition.Value) ?? _fallbackPosition.Value;
                    MoveSmoothlyTo(target);
                    TrySetStanceFromNearbyCover(target);
                    ForceState(CombatState.Fallback, time);
                    _groupSync?.BroadcastFallbackPoint(target);
                    EchoFallbackToSquad();
                    return;
                }
            }

            _fallbackPosition = _bot.Position - _bot.LookDirection.normalized * 5f;
            Vector3 fallback = _squadCoordinator?.ApplyOffsetTo(_fallbackPosition.Value) ?? _fallbackPosition.Value;
            MoveSmoothlyTo(fallback);
            TrySetStanceFromNearbyCover(fallback);
            ForceState(CombatState.Fallback, time);
            _groupSync?.BroadcastFallbackPoint(fallback);
            EchoFallbackToSquad();
        }

        #endregion

        #region Movement

        private void MoveSmoothlyTo(Vector3 destination)
        {
            if (_bot == null || _profile == null)
                return;

            if (!_lastMoveTarget.HasValue || Vector3.Distance(_lastMoveTarget.Value, destination) > MoveTargetTolerance)
            {
                BotMovementHelper.SmoothMoveTo(_bot, destination, false, _profile.Cohesion);
                _lastMoveTarget = destination;
                _lastMoveCommandTime = Time.time;
            }
        }

        private void TrySetStanceFromNearbyCover(Vector3 position)
        {
            if (_cache == null) return;

            Collider[] hits = Physics.OverlapSphere(position, 2f);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent<CustomNavigationPoint>(out var point))
                {
                    if (BotCoverHelper.IsProneCover(point))
                        _cache.PoseController?.SetProne(true);
                    else if (BotCoverHelper.IsLowCover(point))
                        _cache.PoseController?.SetCrouch(true);
                    else
                        _cache.PoseController?.SetStand();
                    return;
                }
            }
        }

        private void TryRepathIfStuck(float time)
        {
            if (_lastMoveTarget.HasValue && _bot?.GetPlayer != null)
            {
                if (_bot.GetPlayer.Velocity.magnitude < 0.1f && time - _lastMoveCommandTime > RepathIfStuckDuration)
                {
                    Vector3 jitter = _lastMoveTarget.Value + UnityEngine.Random.insideUnitSphere * 2f;
                    jitter.y = _bot.Position.y;
                    MoveSmoothlyTo(jitter);
                    _lastMoveCommandTime = time;
                }
            }
        }

        #endregion

        #region Transitions

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

        #endregion

        #region Squad Echo

        private void EchoInvestigateToSquad()
        {
            if (_bot?.BotsGroup == null || Time.time - _lastEchoTime < EchoCooldown)
                return;

            for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
            {
                var teammate = _bot.BotsGroup.Member(i);
                if (teammate == null || teammate == _bot)
                    continue;

                if (Vector3.Distance(teammate.Position, _bot.Position) < 30f && UnityEngine.Random.value < EchoChance)
                {
                    teammate.GetComponent<CombatStateMachine>()?.NotifyEchoInvestigate();
                }
            }

            _lastEchoTime = Time.time;
        }

        private void EchoFallbackToSquad()
        {
            if (_bot?.BotsGroup == null || Time.time - _lastEchoTime < EchoCooldown)
                return;

            for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
            {
                var teammate = _bot.BotsGroup.Member(i);
                if (teammate == null || teammate == _bot)
                    continue;

                if (Vector3.Distance(teammate.Position, _bot.Position) < 30f && UnityEngine.Random.value < EchoChance)
                {
                    teammate.GetComponent<CombatStateMachine>()?.TriggerFallback(teammate.Position - teammate.LookDirection.normalized * 5f);
                }
            }

            _lastEchoTime = Time.time;
        }

        private void EchoSpottedEnemyToSquad(Vector3 pos)
        {
            if (_bot?.BotsGroup == null || _tacticalMemory == null)
                return;

            for (int i = 0; i < _bot.BotsGroup.MembersCount; i++)
            {
                var mate = _bot.BotsGroup.Member(i);
                if (mate == null || mate == _bot)
                    continue;

                if (Vector3.Distance(mate.Position, _bot.Position) < 40f)
                {
                    var mem = mate.GetComponent<BotTacticalMemory>();
                    mem?.RecordEnemyPosition(pos);
                }
            }
        }

        #endregion

        #region Public API

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

        public bool IsInCombatState() => _state != CombatState.Patrol;

        #endregion
    }
}
