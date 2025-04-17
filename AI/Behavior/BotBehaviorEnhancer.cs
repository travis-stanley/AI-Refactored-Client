#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using EFT;
using EFT.InventoryLogic;
using EFT.Interactive;
using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;

namespace AIRefactored.AI.Behavior
{
    public class BotBehaviorEnhancer : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotGroupSyncCoordinator? _groupSync;
        private AIRefactoredBotOwner? _owner;
        private BotPersonalityProfile? _profile;

        private float _lastLootCheck = 0f;
        private float _lastExtractCheck = 0f;
        private float _lastEnemySeenTime = 0f;
        private float _lastMoveCommandTime = -999f;

        private Vector3? _lastMoveTarget = null;

        private bool _isExtracting = false;
        private bool _hasExtracted = false;

        private ExfiltrationPoint[]? _extractPoints;
        private List<Transform>? _lootPoints;

        private const float EXTRACT_RADIUS = 10f;
        private const float GROUP_RADIUS = 12f;
        private const float RETREAT_RADIUS = 16f;
        private const float MOVE_COOLDOWN = 0.25f;

        public void Init(BotOwner bot)
        {
            _bot = bot;
            _cache = bot.GetComponent<BotComponentCache>();
            _groupSync = bot.GetComponent<BotGroupSyncCoordinator>();
            _owner = bot.GetComponent<AIRefactoredBotOwner>();
            _profile = _owner?.PersonalityProfile;

            _extractPoints = GameObject.FindObjectsOfType<ExfiltrationPoint>();
            _lootPoints = new List<Transform>();
            foreach (var container in GameObject.FindObjectsOfType<LootableContainer>())
            {
                if (container.transform != null)
                    _lootPoints.Add(container.transform);
            }
        }

        public void Tick(float time)
        {
            if (_bot == null || _profile == null || _hasExtracted || !_bot.GetPlayer?.HealthController?.IsAlive == true)
                return;

            if (!_bot.GetPlayer.IsAI) return;

            if (_bot.Memory.GoalEnemy?.IsVisible == true)
                _lastEnemySeenTime = time;

            float calmDelay = Mathf.Lerp(10f, 3f, _profile.Caution);
            float lootCooldown = Mathf.Lerp(8f, 2f, _profile.AggressionLevel);
            float extractCooldown = Mathf.Lerp(5f, 1f, _profile.Cohesion);

            if (time - _lastLootCheck > lootCooldown && time - _lastEnemySeenTime > calmDelay)
            {
                TryLoot();
                _lastLootCheck = time;
            }

            if (time - _lastExtractCheck > extractCooldown)
            {
                TryExtract();
                _lastExtractCheck = time;
            }

            TryRetreat();
        }

        private void TryLoot()
        {
            if (_bot == null || _lootPoints == null || _lootPoints.Count == 0)
                return;

            Transform? best = null;
            float bestScore = 0f;

            foreach (var loot in _lootPoints)
            {
                float value = 1f; // future value scaling
                float dist = Vector3.Distance(_bot.Position, loot.position);
                float score = value / Mathf.Max(1f, dist);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = loot;
                }
            }

            if (best != null)
            {
                SmoothMoveTo(best.position);
                _groupSync?.BroadcastLootPoint(best.position);
            }
        }

        private void TryExtract()
        {
            if (_isExtracting || _bot == null || _extractPoints == null || _profile == null)
                return;

            if (!IsGroupReadyToExtract()) return;

            foreach (var point in _extractPoints)
            {
                if (point.Status != EExfiltrationStatus.RegularMode)
                    continue;

                float dist = Vector3.Distance(_bot.Position, point.transform.position);
                if (dist <= EXTRACT_RADIUS)
                {
                    BeginExtract(point);
                    return;
                }
            }

            Vector3? fallback = _groupSync?.GetSharedExtractTarget();
            if (fallback.HasValue)
                SmoothMoveTo(fallback.Value);
        }

        private bool IsGroupReadyToExtract()
        {
            var teammates = _groupSync?.GetTeammates();
            if (teammates == null || teammates.Count <= 1)
                return true;

            int nearCount = 0;
            foreach (var mate in teammates)
            {
                if (mate != null && mate != _bot && Vector3.Distance(mate.Position, _bot.Position) <= GROUP_RADIUS)
                    nearCount++;
            }

            return nearCount >= Mathf.CeilToInt(teammates.Count * _profile!.Cohesion);
        }

        private void BeginExtract(ExfiltrationPoint point)
        {
            _isExtracting = true;
            _groupSync?.BroadcastExtractPoint(point.transform.position);
            SmoothMoveTo(point.transform.position);
            _bot?.BotTalk?.TrySay(EPhraseTrigger.MumblePhrase);
            _bot?.Deactivate();
            StartCoroutine(ExtractRoutine(6f));
        }

        private IEnumerator ExtractRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            _hasExtracted = true;
            if (_bot?.GetPlayer != null)
                _bot.GetPlayer.gameObject.SetActive(false);
        }

        private void TryRetreat()
        {
            if (_bot == null || _profile == null || !_bot.Memory.IsUnderFire)
                return;

            var hc = _bot.GetPlayer?.HealthController;
            if (hc == null) return;

            float current = 0f, max = 0f;
            foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
            {
                var hp = hc.GetBodyPartHealth(part);
                current += hp.Current;
                max += hp.Maximum;
            }

            float ratio = max > 0f ? current / max : 1f;
            if (ratio >= _profile.RetreatThreshold) return;

            Vector3 fallback = _bot.Position - _bot.Memory.GoalEnemy.CurrPosition.normalized * 8f;
            SmoothMoveTo(fallback);
            _groupSync?.BroadcastExtractPoint(fallback);

            var squad = _groupSync?.GetTeammates();
            if (squad != null)
            {
                foreach (var other in squad)
                {
                    if (other != null && other != _bot &&
                        Vector3.Distance(_bot.Position, other.Position) <= RETREAT_RADIUS &&
                        UnityEngine.Random.value < _profile.Cohesion)
                    {
                        Vector3 retreat = Vector3.Lerp(other.Position, fallback, 0.6f);
                        other.Mover?.GoToPoint(retreat, false, 1f);
                    }
                }
            }
        }

        private void SmoothMoveTo(Vector3 target)
        {
            if (_bot == null || Time.time < _lastMoveCommandTime + MOVE_COOLDOWN)
                return;

            float distance = Vector3.Distance(_bot.Position, target);
            if (distance < 1.25f)
                return;

            float damping = Mathf.Lerp(0.25f, 0.65f, _profile?.AggressionLevel ?? 0.5f);
            Vector3 smoothTarget = Vector3.Lerp(_bot.Position, target, damping);

            _bot.Mover?.GoToPoint(smoothTarget, false, 1f);
            _lastMoveCommandTime = Time.time;
            _lastMoveTarget = target;
        }
    }
}
