#nullable enable

using UnityEngine;
using EFT;
using System.Collections.Generic;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Components;
using Comfort.Common;
using System;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Simulates realistic directional hearing for bots.
    /// Includes sound detection and deafening from loud sources.
    /// </summary>
    public class BotHearingSystem : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private AIRefactoredBotOwner? _owner;
        private HearingDamageComponent? _hearing;

        private float _nextCheckTime = 0f;
        private const float CheckInterval = 0.3f;

        private const float MaxBaseHearing = 60f;
        private const float SprintLoudness = 1.0f;
        private const float WalkLoudness = 0.6f;
        private const float CrouchLoudness = 0.3f;
        private const float FireLoudness = 1.25f;

        private int _playerLayerMask;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _owner = GetComponent<AIRefactoredBotOwner>();
            _hearing = GetComponent<HearingDamageComponent>();

            _playerLayerMask = LayerMask.GetMask("Player", "PlayerCorpse", "Ragdoll", "Interactive");
        }

        private void Update()
        {
            if (_bot == null || _cache == null || _owner == null || Time.time < _nextCheckTime || _bot.IsDead)
                return;

            _nextCheckTime = Time.time + CheckInterval;
            EvaluateNearbySounds();
        }

        private void EvaluateNearbySounds()
        {
            float hearingMod = Mathf.Lerp(0.5f, 1.5f, _owner.PersonalityProfile?.Caution ?? 0.5f);
            float effectiveRadius = MaxBaseHearing * hearingMod;

            Collider[] hits = Physics.OverlapSphere(_bot.Position, effectiveRadius, _playerLayerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var player = hits[i].GetComponent<Player>();
                if (player == null || player.ProfileId == _bot.ProfileId || !player.HealthController.IsAlive)
                    continue;

                var ctx = player.MovementContext;
                float loudness = 0f;

                if (ctx?.CurrentState != null)
                {
                    var stateName = ctx.CurrentState.GetType().Name;
                    if (stateName.Contains("Sprint"))
                        loudness = SprintLoudness;
                    else if (stateName.Contains("Walk"))
                        loudness = WalkLoudness;
                    else if (stateName.Contains("Crouch"))
                        loudness = CrouchLoudness;
                }

                float distance = Vector3.Distance(player.Position, _bot.Position);

                // Gunfire fallback for nearby players
                bool loudGunfireFallback = distance < 50f && !player.IsAI;
                if (loudGunfireFallback)
                {
                    loudness = Mathf.Max(loudness, FireLoudness);
                }

                // Deafening logic
                if (loudness >= 1f)
                {
                    float scaled = Mathf.Clamp01(1f - (distance / 30f));
                    if (scaled > 0.2f)
                    {
                        float intensity = scaled;
                        float duration = Mathf.Lerp(1f, 8f, scaled);

                        var equipment = _bot.Profile?.Inventory?.Equipment;
                        var earpiece = equipment?.GetSlot(EFT.InventoryLogic.EquipmentSlot.Earpiece)?.ContainedItem;
                        bool hasEarProtection = earpiece != null;

                        if (hasEarProtection)
                        {
                            intensity *= 0.3f;
                            duration *= 0.4f;
                        }

                        _hearing?.ApplyDeafness(intensity, duration);
                    }
                }

                if (loudness <= 0.1f)
                    continue;

                float perceived = loudness * Mathf.Clamp01(1f - (distance / effectiveRadius));

                if (!HasClearPath(player.Position, out float occlusionMod))
                    perceived *= occlusionMod;

                if (_hearing != null && _hearing.IsDeafened)
                    perceived *= Mathf.Lerp(0.1f, 0.5f, _hearing.Deafness);

                if (perceived > 0.35f)
                {
                    _cache.RegisterHeardSound(player.Position); // 🧠 log it

                    HandleDetectedNoise(player.Position, perceived);
#if UNITY_EDITOR
                    Debug.DrawLine(_bot.Position, player.Position, Color.yellow, 0.25f);
                    Debug.Log($"[AIRefactored-Hearing] {_bot.Profile?.Info?.Nickname} heard noise → loudness={perceived:F2}");
#endif
                    break;
                }
            }
        }

        private bool HasClearPath(Vector3 source, out float occlusionModifier)
        {
            occlusionModifier = 1f;
            if (Physics.Linecast(source, _bot!.Position, out RaycastHit hit))
            {
                if (hit.collider != null && hit.collider.gameObject != _bot.GetPlayer?.gameObject)
                {
                    occlusionModifier = 0.25f;
                    return false;
                }
            }
            return true;
        }

        private void HandleDetectedNoise(Vector3 position, float loudness)
        {
            if (_bot.Memory.GoalEnemy != null)
                return;

            Vector3 alertPoint = _bot.Position + (position - _bot.Position).normalized * 3f;

            _bot.GoToPoint(alertPoint, false);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyShot);
        }
    }
}
