#nullable enable

using System;
using System.Collections;
using System.Collections.Generic;
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

        private bool _isExtracting = false;
        private bool _hasExtracted = false;

        private const float LOOT_RADIUS = 8f;
        private const float EXTRACT_RADIUS = 10f;
        private const float GROUP_RADIUS = 12f;
        private const float RETREAT_RADIUS = 16f;
        private const float BASE_LOOT_COOLDOWN = 8f;
        private const float BASE_EXTRACT_COOLDOWN = 5f;
        private const float BASE_CALM_DELAY = 10f;

        public void Init(BotOwner bot)
        {
            _bot = bot;
            _cache = bot.GetComponent<BotComponentCache>();
            _groupSync = bot.GetComponent<BotGroupSyncCoordinator>();
            _owner = bot.GetComponent<AIRefactoredBotOwner>();
            _profile = _owner?.PersonalityProfile;
        }

        public void Tick(float time)
        {
            if (_bot == null || _profile == null || _hasExtracted || !_bot.GetPlayer?.HealthController?.IsAlive == true)
                return;

            if (_bot.Memory.GoalEnemy?.IsVisible == true)
                _lastEnemySeenTime = time;

            float calmDelay = Mathf.Lerp(BASE_CALM_DELAY, 3f, _profile.Caution);
            float lootCooldown = Mathf.Lerp(BASE_LOOT_COOLDOWN, 2f, _profile.AggressionLevel);
            float extractCooldown = Mathf.Lerp(BASE_EXTRACT_COOLDOWN, 1f, _profile.Cohesion);

            if (time - _lastLootCheck > lootCooldown && time - _lastEnemySeenTime > calmDelay)
            {
                EvaluateLootClusters();
                _lastLootCheck = time;
            }

            if (time - _lastExtractCheck > extractCooldown)
            {
                TryExtract();
                _lastExtractCheck = time;
            }

            EvaluateRetreat();
        }

        private void EvaluateLootClusters()
        {
            UnityEngine.Object[] found = Resources.FindObjectsOfTypeAll(typeof(UnityEngine.Object));
            Vector3? bestPoint = null;
            float bestScore = 0f;

            for (int i = 0; i < found.Length; i++)
            {
                var obj = found[i];
                if (obj == null || obj.GetType().Name != "AILootPointsCluster")
                    continue;

                var component = obj as Component;
                if (component == null)
                    continue;

                var clusterTransform = component.transform;

                var field = obj.GetType().GetField("PatrolPoints");
                if (field == null)
                    continue;

                var points = field.GetValue(obj) as IEnumerable;
                if (points == null)
                    continue;

                float clusterValue = 0f;
                foreach (var pt in points)
                {
                    var itemTemplateField = pt?.GetType().GetField("Template");
                    var template = itemTemplateField?.GetValue(pt) as ItemTemplate;
                    if (template != null)
                    {
                        float value = template.CreditsPrice;

                        // Rarity boost for high value or rare items
                        if (value > 30000)
                            value *= 2.5f;
                        else if (value > 15000)
                            value *= 1.5f;

                        clusterValue += value;
                    }
                }

                float dist = Vector3.Distance(_bot!.Position, clusterTransform.position);
                float score = clusterValue / Mathf.Max(dist, 1f);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPoint = clusterTransform.position;
                }
            }

            if (bestPoint.HasValue)
                MoveToLootPoint(bestPoint.Value);
        }

        private void MoveToLootPoint(Vector3 point)
        {
            EnsureDoorOpen(point);
            _bot?.Mover?.GoToPoint(point, false, 1f);
            _groupSync?.BroadcastLootPoint(point);
        }

        private void EnsureDoorOpen(Vector3 point)
        {
            if (_bot == null)
                return;

            Vector3 dir = (point - _bot.Position).normalized;
            if (Physics.Raycast(_bot.Position, dir, out RaycastHit hit, 2f))
            {
                if (hit.collider.TryGetComponent(out Door door) && door.DoorState != EDoorState.Open)
                {
                    door.Interact(new InteractionResult(EInteractionType.Open));
                }
            }
        }

        private void TryExtract()
        {
            if (_isExtracting || _bot == null || _profile == null)
                return;

            if (!IsGroupAlignedForExtract())
                return;

            ExfiltrationPoint[] extracts = GameObject.FindObjectsOfType<ExfiltrationPoint>();
            for (int i = 0; i < extracts.Length; i++)
            {
                var point = extracts[i];
                if (point.Status != EExfiltrationStatus.RegularMode)
                    continue;

                float dist = Vector3.Distance(_bot.Position, point.transform.position);
                if (dist <= EXTRACT_RADIUS)
                {
                    BeginExtract(point);
                    return;
                }
            }

            Vector3? shared = _groupSync?.GetSharedExtractTarget();
            if (shared.HasValue)
                _bot.Mover?.GoToPoint(shared.Value, false, 1f);
        }

        private bool IsGroupAlignedForExtract()
        {
            var squad = _groupSync?.GetTeammates();
            if (squad == null || squad.Count <= 1)
                return true;

            int nearby = 0;
            for (int i = 0; i < squad.Count; i++)
            {
                var teammate = squad[i];
                if (teammate != null && teammate != _bot)
                {
                    float dist = Vector3.Distance(teammate.Position, _bot.Position);
                    if (dist < GROUP_RADIUS)
                        nearby++;
                }
            }

            return nearby >= Mathf.CeilToInt(squad.Count * _profile!.Cohesion);
        }

        private void BeginExtract(ExfiltrationPoint point)
        {
            _isExtracting = true;
            _groupSync?.BroadcastExtractPoint(point.transform.position);
            _bot.Mover?.GoToPoint(point.transform.position, false, 1f);
            _bot.BotTalk?.TrySay(EPhraseTrigger.MumblePhrase);
            _bot.Deactivate();
            StartCoroutine(ExtractCoroutine(6f));
        }

        private IEnumerator ExtractCoroutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            _hasExtracted = true;
            if (_bot?.GetPlayer != null)
                _bot.GetPlayer.gameObject.SetActive(false);
        }

        private void EvaluateRetreat()
        {
            if (_profile == null || _bot == null || !_bot.Memory.IsUnderFire)
                return;

            float current = 0f, max = 0f;
            foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
            {
                var hp = _bot.GetPlayer!.HealthController.GetBodyPartHealth(part);
                current += hp.Current;
                max += hp.Maximum;
            }

            if (max <= 0f)
                return;

            float ratio = current / max;
            if (ratio < _profile.RetreatThreshold)
            {
                Vector3 retreat = _bot.Position - _bot.Memory.GoalEnemy.CurrPosition;
                Vector3 fallback = _bot.Position + retreat.normalized * 10f;

                _bot.Mover?.GoToPoint(fallback, false, 1f);
                _groupSync?.BroadcastExtractPoint(fallback);

                var teammates = _groupSync?.GetTeammates();
                if (teammates != null)
                {
                    for (int i = 0; i < teammates.Count; i++)
                    {
                        var other = teammates[i];
                        if (other != null && other != _bot)
                        {
                            float dist = Vector3.Distance(other.Position, _bot.Position);
                            if (dist < RETREAT_RADIUS && UnityEngine.Random.value < _profile.Cohesion)
                            {
                                other.Mover?.GoToPoint(fallback, false, 1f);
                            }
                        }
                    }
                }
            }
        }
    }
}
