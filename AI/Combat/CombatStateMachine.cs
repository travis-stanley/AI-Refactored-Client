#nullable enable

using AIRefactored.AI.Components;
using AIRefactored.AI.Combat.States;
using AIRefactored.AI.Core;
using EFT;
using System;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Core AI controller that evaluates modular combat decision layers:
    /// Patrol, Investigate, Engage, Attack, and Fallback.
    /// Each layer defines its own eligibility via ShallUseNow().
    /// </summary>
    public sealed class CombatStateMachine
    {
        #region Fields

        private BotComponentCache _cache = null!;
        private BotOwner _bot = null!;
        private float _lastStateChangeTime;
        private Vector3? _lastKnownEnemyPos;

        private PatrolHandler? _patrol;
        private InvestigateHandler? _investigate;
        private EngageHandler? _engage;
        private AttackHandler? _attack;
        private FallbackHandler? _fallback;
        private EchoCoordinator? _echo;

        private const float MinTransitionDelay = 0.4f;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot!;

            _patrol = new PatrolHandler(cache);
            _investigate = new InvestigateHandler(cache);
            _engage = new EngageHandler(cache);
            _attack = new AttackHandler(cache);
            _fallback = new FallbackHandler(cache);
            _echo = new EchoCoordinator(cache);
        }

        #endregion

        #region Main Tick

        public void Tick(float time)
        {
            if (_bot.IsDead || !_bot.GetPlayer?.IsAI == true)
                return;

            if (time - _lastStateChangeTime < MinTransitionDelay)
                return;

            var enemy = _cache.ThreatSelector?.CurrentTarget ?? _bot.Memory?.GoalEnemy?.Person;

            if (enemy != null && enemy.HealthController?.IsAlive == true)
            {
                _lastKnownEnemyPos = enemy.Transform.position;
                _cache.TacticalMemory?.RecordEnemyPosition(_lastKnownEnemyPos.Value);

                if (!_bot.Mover.Sprinting)
                {
                    _bot.Sprint(true);
                    _echo?.EchoSpottedEnemyToSquad(_lastKnownEnemyPos.Value);
                }
            }

            // === Layer evaluation order ===

            // 1. Forced fallback from suppression or panic
            if (_fallback?.ShouldTriggerSuppressedFallback(time, _lastStateChangeTime, 1.25f) == true)
            {
                Vector3 retreat = _fallback.GetFallbackPosition();
                _fallback.SetFallbackTarget(retreat);
                _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
                _echo?.EchoFallbackToSquad(retreat);
                _lastStateChangeTime = time;
                return;
            }

            if (_fallback!.ShallUseNow(time))
            {
                _fallback.Tick(time, _lastKnownEnemyPos, (_, t) => _lastStateChangeTime = t);
                return;
            }

            // 2. Engage → Attack logic
            if (_engage!.ShallUseNow())
            {
                if (_engage.CanAttack())
                {
                    _attack!.Tick(time);
                }
                else
                {
                    _engage.Tick();
                }

                _lastStateChangeTime = time;
                return;
            }

            // 3. Investigate logic from sound or remembered enemy
            if (_investigate!.ShallUseNow(time, _lastStateChangeTime))
            {
                Vector3? target = _investigate.GetInvestigateTarget(_lastKnownEnemyPos);
                if (target.HasValue)
                {
                    _investigate.Investigate(target.Value);
                }

                _lastStateChangeTime = time;
                return;
            }

            // 4. Patrol as default behavior
            _patrol!.Tick(time);
        }

        #endregion

        #region Public Triggers

        public void NotifyDamaged()
        {
            float time = Time.time;

            if (_fallback != null && !_fallback.ShallUseNow(time))
            {
                _lastStateChangeTime = time;
                _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
            }
        }

        public void NotifyEchoInvestigate()
        {
            float time = Time.time;
            if (_investigate != null && !_investigate.ShallUseNow(time, _lastStateChangeTime))
            {
                _lastStateChangeTime = time;
                _bot.BotTalk?.TrySay(EPhraseTrigger.Cooperation);
            }
        }

        public void TriggerFallback(Vector3 fallbackPos)
        {
            float time = Time.time;
            _fallback?.SetFallbackTarget(fallbackPos);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);
            _lastStateChangeTime = time;
        }

        #endregion

        #region Utility

        public bool IsInCombatState() => _cache.ThreatSelector?.CurrentTarget != null;

        public float LastStateChangeTime => _lastStateChangeTime;
        public Vector3? LastKnownEnemyPos => _lastKnownEnemyPos;

        internal void TrySetStanceFromNearbyCover(Vector3 position)
        {
            _cache?.PoseController?.TrySetStanceFromNearbyCover(position);
        }

        #endregion
    }
}
