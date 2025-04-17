#nullable enable

using UnityEngine;
using EFT;
using Comfort.Common;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Helpers;
using System.Collections.Generic;
using AIRefactored.AI.Optimization;

namespace AIRefactored.AI.Combat
{
    public class CombatStateMachine : MonoBehaviour
    {
        #region Enums

        private enum CombatState
        {
            Patrol,
            Investigate,
            Attack,
            Fallback
        }

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private AIRefactoredBotOwner? _owner;
        private BotPersonalityProfile? _profile;
        private BotSuppressionReactionComponent? _suppress;

        private CombatState _state = CombatState.Patrol;
        private Vector3? _fallbackPosition;

        private float _lastHitTime = -999f;
        private float _switchCooldown = 0f;

        private const float InvestigateScanRadius = 4f;
        private const float InvestigateCooldown = 10f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _owner = GetComponent<AIRefactoredBotOwner>();
            _profile = _owner?.PersonalityProfile;
            _suppress = GetComponent<BotSuppressionReactionComponent>();
        }

        private void Update()
        {
            if (_bot == null || _cache == null || _bot.IsDead)
                return;

            if (_bot.GetPlayer != null && !_bot.GetPlayer.IsAI)
                return; // 🛑 Ignore human-controlled players (e.g., FIKA coop)

            float time = Time.time;

            // === Engage: Enemy in sight ===
            if (_bot.Memory?.GoalEnemy != null)
            {
                _state = CombatState.Attack;
                _bot.Sprint(true);
                return;
            }

            // === Suppression fallback ===
            if (_suppress?.IsSuppressed() == true && _state != CombatState.Fallback)
            {
                _state = CombatState.Fallback;
                _fallbackPosition = null;
                _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);

                float cohesion = 1.0f;
                var personality = BotRegistry.Get(_bot.ProfileId);
                if (personality != null)
                    cohesion = Mathf.Lerp(0.6f, 1.2f, personality.Cohesion);

                if (_cache.PathCache != null)
                {
                    Vector3 threatDir = _bot.LookDirection.normalized;
                    List<Vector3> path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, threatDir, _cache.PathCache);
                    if (path.Count > 0)
                    {
                        _fallbackPosition = path[path.Count - 1];
                        BotMovementHelper.SmoothMoveTo(_bot, _fallbackPosition.Value, allowSlowEnd: false, cohesionScale: cohesion);
                    }
                }
                else
                {
                    _fallbackPosition = _bot.Position - _bot.LookDirection.normalized * 5f;
                    BotMovementHelper.SmoothMoveTo(_bot, _fallbackPosition.Value, allowSlowEnd: false, cohesionScale: cohesion);
                }
            }

            // === Handle fallback movement ===
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
                    BotMovementHelper.SmoothMoveTo(_bot, _fallbackPosition.Value);
                }
                return;
            }

            // === Investigate → Timeout back to patrol ===
            if (_state == CombatState.Investigate && time - _lastHitTime > InvestigateCooldown)
            {
                _state = CombatState.Patrol;
            }

            // === Sound-based suspicion triggers investigation ===
            if (_profile != null &&
                _cache.LastHeardTime + 4f > time &&
                _state == CombatState.Patrol &&
                _profile.Caution > 0.6f)
            {
                _state = CombatState.Investigate;
                _lastHitTime = time;
                _bot.BotTalk?.TrySay(EPhraseTrigger.NeedHelp);
            }

            // === Patrol movement ===
            if (_state == CombatState.Patrol && time >= _switchCooldown)
            {
                Vector3 target = HotspotSystem.GetRandomHotspot(_bot);
                BotMovementHelper.SmoothMoveTo(_bot, target);
                _switchCooldown = time + Random.Range(15f, 45f);

                if (Random.value < 0.25f)
                    _bot.BotTalk?.TrySay(EPhraseTrigger.GoForward);
            }

            // === Investigate jitter scanning ===
            if (_state == CombatState.Investigate)
            {
                Vector3 jitter = Random.insideUnitSphere * InvestigateScanRadius;
                Vector3 scanPoint = _bot.Position + new Vector3(jitter.x, 0f, jitter.z);
                BotMovementHelper.SmoothMoveTo(_bot, scanPoint);

                if (Random.value < 0.3f)
                    _bot.BotTalk?.TrySay(EPhraseTrigger.Look);
            }
        }

        #endregion

        #region Public API

        public void NotifyDamaged()
        {
            _lastHitTime = Time.time;
            _state = CombatState.Investigate;
            _bot?.BotTalk?.TrySay(EPhraseTrigger.MumblePhrase);
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

        #endregion
    }
}
