#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using EFT;
using EFT.Interactive;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Behavior
{
    /// <summary>
    /// Enhances bot behavior with looting, retreat, and group-aware extraction logic.
    /// Controlled via periodic Tick() updates from BotBrain.
    /// </summary>
    public class BotBehaviorEnhancer : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotGroupSyncCoordinator? _groupSync;
        private AIRefactoredBotOwner? _owner;
        private BotPersonalityProfile? _profile;

        private float _lastLootCheck = 0f;
        private float _lastExtractCheck = 0f;
        private float _lastEnemySeenTime = 0f;
        private float _lastTickTime = -999f;

        private bool _isExtracting = false;
        private bool _hasExtracted = false;

        private ExfiltrationPoint[]? _extractPoints;
        private readonly List<Transform> _lootPoints = new(64);

        private const float EXTRACT_RADIUS = 10f;
        private const float GROUP_RADIUS = 12f;
        private const float RETREAT_RADIUS = 16f;
        private const float TickInterval = 0.25f;

        #endregion

        #region Initialization

        public void Init(BotOwner bot)
        {
            _bot = bot;
            _cache = bot.GetComponent<BotComponentCache>();
            _groupSync = bot.GetComponent<BotGroupSyncCoordinator>();
            _owner = bot.GetComponent<AIRefactoredBotOwner>();
            _profile = _owner?.PersonalityProfile;

            _extractPoints = GameObject.FindObjectsOfType<ExfiltrationPoint>();

            _lootPoints.Clear();
            foreach (var loot in GameObject.FindObjectsOfType<LootableContainer>())
            {
                if (loot != null)
                    _lootPoints.Add(loot.transform);
            }
        }

        #endregion

        #region Tick API

        public void Tick(float time)
        {
            if (_bot == null || _profile == null || _hasExtracted || _bot.GetPlayer?.HealthController?.IsAlive != true)
                return;

            if (!_bot.GetPlayer.IsAI || time - _lastTickTime < TickInterval)
                return;

            _lastTickTime = time;

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

        #endregion

        #region Loot Logic

        private void TryLoot()
        {
            if (_bot == null || _lootPoints.Count == 0)
                return;

            Transform? best = null;
            float bestScore = 0f;
            Vector3 pos = _bot.Position;

            foreach (var loot in _lootPoints)
            {
                float dist = Vector3.Distance(pos, loot.position);
                float score = 1f / Mathf.Max(1f, dist);
                if (score > bestScore)
                {
                    best = loot;
                    bestScore = score;
                }
            }

            if (best != null)
            {
                BotMovementHelper.SmoothMoveTo(_bot, best.position, false, _profile?.Cohesion ?? 1f);
                _groupSync?.BroadcastLootPoint(best.position);
            }
        }

        #endregion

        #region Extract Logic

        private void TryExtract()
        {
            if (_isExtracting || _bot == null || _extractPoints == null || _profile == null)
                return;

            if (!IsGroupReadyToExtract())
                return;

            foreach (var point in _extractPoints)
            {
                if (point == null || point.Status != EExfiltrationStatus.RegularMode)
                    continue;

                if (Vector3.Distance(_bot.Position, point.transform.position) <= EXTRACT_RADIUS)
                {
                    BeginExtract(point);
                    return;
                }
            }

            Vector3? fallback = _groupSync?.GetSharedExtractTarget();
            if (fallback.HasValue)
                BotMovementHelper.SmoothMoveTo(_bot, fallback.Value, false, _profile.Cohesion);
        }

        private void BeginExtract(ExfiltrationPoint point)
        {
            _isExtracting = true;
            _groupSync?.BroadcastExtractPoint(point.transform.position);
            BotMovementHelper.SmoothMoveTo(_bot!, point.transform.position, false, _profile?.Cohesion ?? 1f);
            _bot?.BotTalk?.TrySay(EPhraseTrigger.MumblePhrase);
            _bot?.Deactivate();

            if (isActiveAndEnabled)
                StartCoroutine(ExtractRoutine(6f));
        }

        private IEnumerator ExtractRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);
            _hasExtracted = true;

            if (_bot?.GetPlayer != null)
                _bot.GetPlayer.gameObject.SetActive(false);
        }

        private bool IsGroupReadyToExtract()
        {
            if (_groupSync == null || _profile == null)
                return true;

            var teammates = _groupSync.GetTeammates();
            if (teammates.Count <= 1)
                return true;

            int near = 0;
            Vector3 botPos = _bot!.Position;

            foreach (var mate in teammates)
            {
                if (mate != null && mate != _bot && Vector3.Distance(botPos, mate.Position) <= GROUP_RADIUS)
                    near++;
            }

            return near >= Mathf.CeilToInt(teammates.Count * _profile.Cohesion);
        }

        #endregion

        #region Retreat Logic

        private void TryRetreat()
        {
            if (_bot == null || _profile == null || !_bot.Memory.IsUnderFire)
                return;

            var hc = _bot.GetPlayer?.HealthController;
            if (hc == null)
                return;

            float current = 0f;
            float max = 0f;

            foreach (EBodyPart part in Enum.GetValues(typeof(EBodyPart)))
            {
                var hp = hc.GetBodyPartHealth(part);
                current += hp.Current;
                max += hp.Maximum;
            }

            float ratio = max > 0f ? current / max : 1f;
            if (ratio >= _profile.RetreatThreshold)
                return;

            Vector3 fallback = _bot.Memory.GoalEnemy != null
                ? _bot.Position - _bot.Memory.GoalEnemy.CurrPosition.normalized * 8f
                : _bot.Position;

            BotMovementHelper.SmoothMoveTo(_bot, fallback, false, _profile.Cohesion);
            _groupSync?.BroadcastExtractPoint(fallback);

            var squad = _groupSync?.GetTeammates();
            if (squad == null)
                return;

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

        #endregion
    }
}
