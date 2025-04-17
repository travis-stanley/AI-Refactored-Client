#nullable enable

using UnityEngine;
using EFT;
using System;
using System.Collections.Generic;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Components;
using Comfort.Common;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Simulates realistic directional hearing for bots.
    /// Includes sound detection, occlusion dampening, deafening, and memory integration.
    /// </summary>
    public class BotHearingSystem : MonoBehaviour
    {
        #region Fields

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

        private static readonly int PlayerLayerMask = LayerMask.GetMask("Player", "PlayerCorpse", "Ragdoll", "Interactive");

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _owner = GetComponent<AIRefactoredBotOwner>();
            _hearing = GetComponent<HearingDamageComponent>();
        }

        private void Update()
        {
            if (_bot == null || _cache == null || _owner == null || _bot.IsDead || Time.time < _nextCheckTime)
                return;

            if (_bot.GetPlayer != null && _bot.GetPlayer.IsYourPlayer)
                return; // 🛑 Ignore human or FIKA-controlled players

            _nextCheckTime = Time.time + CheckInterval;
            EvaluateNearbySounds();
        }

        #endregion

        #region Hearing Logic

        private void EvaluateNearbySounds()
        {
            float caution = _owner.PersonalityProfile?.Caution ?? 0.5f;
            float effectiveRadius = MaxBaseHearing * Mathf.Lerp(0.5f, 1.5f, caution);

            Collider[] hits = Physics.OverlapSphere(_bot!.Position, effectiveRadius, PlayerLayerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                var player = hits[i].GetComponent<Player>();
                if (player == null || player.ProfileId == _bot.ProfileId || !player.HealthController.IsAlive)
                    continue;

                if (!player.IsAI && player.IsYourPlayer)
                    continue;

                float loudness = EstimateLoudness(player);
                if (loudness <= 0.1f)
                    continue;

                float distance = Vector3.Distance(_bot.Position, player.Position);

                // Fallback for unknown sound events (e.g., gunfire)
                if (!player.IsAI && distance < 50f)
                    loudness = Mathf.Max(loudness, FireLoudness);

                // Deafening from loud sources
                if (loudness >= 1f)
                    TryApplyDeafness(distance);

                float perceived = loudness * Mathf.Clamp01(1f - (distance / effectiveRadius));

                if (!HasClearPath(player.Position, out float occlusionMod))
                    perceived *= occlusionMod;

                if (_hearing != null && _hearing.IsDeafened)
                    perceived *= Mathf.Lerp(0.1f, 0.5f, _hearing.Deafness);

                if (perceived > 0.35f)
                {
                    _cache.RegisterHeardSound(player.Position);
                    _bot.BotsGroup?.LastSoundsController?.AddNeutralSound(player, player.Position);
                    HandleDetectedNoise(player.Position);
                    return;
                }
            }
        }

        private float EstimateLoudness(Player player)
        {
            string? stateName = player.MovementContext?.CurrentState?.GetType().Name;
            if (string.IsNullOrEmpty(stateName))
                return 0f;

            if (stateName.Contains("Sprint")) return SprintLoudness;
            if (stateName.Contains("Walk")) return WalkLoudness;
            if (stateName.Contains("Crouch")) return CrouchLoudness;
            return 0f;
        }

        private void TryApplyDeafness(float distance)
        {
            if (_hearing == null || distance > 30f)
                return;

            float scaled = Mathf.Clamp01(1f - (distance / 30f));
            if (scaled <= 0.2f)
                return;

            float intensity = scaled;
            float duration = Mathf.Lerp(1f, 8f, scaled);

            var equipment = _bot?.Profile?.Inventory?.Equipment;
            var earpiece = equipment?.GetSlot(EFT.InventoryLogic.EquipmentSlot.Earpiece)?.ContainedItem;
            bool hasEarProtection = earpiece != null;

            if (hasEarProtection)
            {
                intensity *= 0.3f;
                duration *= 0.4f;
            }

            _hearing.ApplyDeafness(intensity, duration);
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

        private void HandleDetectedNoise(Vector3 position)
        {
            if (_bot?.Memory.GoalEnemy != null)
                return;

            Vector3 direction = (position - _bot.Position).normalized;
            Vector3 alertPoint = _bot.Position + direction * 3f;

            _bot.GoToPoint(alertPoint, false);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyShot);
        }

        #endregion
    }
}
