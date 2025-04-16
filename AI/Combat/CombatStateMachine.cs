#nullable enable

using UnityEngine;
using EFT;
using Comfort.Common;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Hotspots;
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Manages the bot's current combat state: patrol, investigate, attack, or fallback.
    /// </summary>
    public class CombatStateMachine : MonoBehaviour
    {
        private enum CombatState
        {
            Patrol,
            Investigate,
            Attack,
            Fallback
        }

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

            float time = Time.time;

            // Forced attack
            if (_bot.Memory.GoalEnemy != null)
            {
                _state = CombatState.Attack;
                _bot.Sprint(true);
                return;
            }

            // Forced fallback if suppressed
            if (_suppress?.IsSuppressed() == true)
            {
                _state = CombatState.Fallback;
                if (!_fallbackPosition.HasValue)
                {
                    Vector3 dir = -_bot.LookDirection.normalized;
                    _fallbackPosition = _bot.Position + dir * 5f;
                }
            }

            // Fallback behavior
            if (_state == CombatState.Fallback)
            {
                if (_fallbackPosition.HasValue)
                {
                    float dist = Vector3.Distance(_bot.Position, _fallbackPosition.Value);
                    if (dist < 2f)
                    {
                        _state = CombatState.Patrol;
                        _fallbackPosition = null;
                    }
                    else
                    {
                        _bot.GoToPoint(_fallbackPosition.Value, false);
                    }
                }
                return;
            }

            // Investigate state timeout
            if (_state == CombatState.Investigate && time - _lastHitTime > InvestigateCooldown)
            {
                _state = CombatState.Patrol;
            }

            // Investigate triggered by sound
            if (_profile != null && _cache.LastHeardTime + 4f > time && _state == CombatState.Patrol && _profile.Caution > 0.6f)
            {
                _state = CombatState.Investigate;
                _lastHitTime = time;
            }

            // Patrol → Hotspot
            if (_state == CombatState.Patrol && time >= _switchCooldown)
            {
                Vector3 target = HotspotSystem.GetRandomHotspot(_bot);
                _bot.GoToPoint(target, slowAtTheEnd: true);

                _switchCooldown = time + Random.Range(15f, 45f);

                if (_bot.BotTalk != null && Random.value < 0.25f)
                    _bot.BotTalk.TrySay(EPhraseTrigger.GoForward);
            }

            // Investigate = scan
            if (_state == CombatState.Investigate)
            {
                Vector3 jitter = Random.insideUnitSphere * InvestigateScanRadius;
                Vector3 scanPoint = _bot.Position + new Vector3(jitter.x, 0f, jitter.z);

                _bot.GoToPoint(scanPoint, false);

                if (_bot.BotTalk != null && Random.value < 0.3f)
                    _bot.BotTalk.TrySay(EPhraseTrigger.Look);
            }
        }

        /// <summary>
        /// Triggered externally when the bot takes damage.
        /// </summary>
        public void NotifyDamaged()
        {
            _lastHitTime = Time.time;
            _state = CombatState.Investigate;

            if (_bot?.BotTalk != null)
                _bot.BotTalk.TrySay(EPhraseTrigger.MumblePhrase);
        }

        /// <summary>
        /// Triggered externally when the bot must fallback.
        /// </summary>
        public void TriggerFallback(Vector3 position)
        {
            _fallbackPosition = position;
            _state = CombatState.Fallback;

            if (_bot?.BotTalk != null)
                _bot.BotTalk.TrySay(EPhraseTrigger.OnLostVisual);
        }
    }
}
