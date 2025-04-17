#nullable enable

using System.Collections.Generic;
using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;

namespace AIRefactored.AI.Groups
{
    public class BotGroupSyncCoordinator : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotsGroup? _group;

        private readonly Dictionary<BotOwner, BotComponentCache> _teammateCaches = new();
        private float _nextSyncTime = 0f;
        private const float SyncInterval = 0.5f;

        private Vector3? _lastLootPoint;
        private Vector3? _lastExtractPoint;

        private static readonly List<BotOwner> _tempList = new();

        #endregion

        #region Initialization

        public void Init(BotOwner bot)
        {
            _bot = bot;
            _cache = _bot?.GetComponent<BotComponentCache>();
            _group = _bot?.BotsGroup;

            if (_bot?.GetPlayer?.IsAI != true)
                return;

            RegisterGroupHooks();
        }

        private void Start()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _group = _bot?.BotsGroup;

            if (_bot?.GetPlayer?.IsAI != true)
                return;

            RegisterGroupHooks();
        }

        private void OnDestroy()
        {
            if (_group != null)
            {
                _group.OnMemberAdd -= OnTeammateAdded;
                _group.OnMemberRemove -= OnTeammateRemoved;
            }
        }

        #endregion

        #region Group Hook Registration

        private void RegisterGroupHooks()
        {
            if (_group == null || _bot == null)
                return;

            _group.OnMemberAdd += OnTeammateAdded;
            _group.OnMemberRemove += OnTeammateRemoved;

            for (int i = 0; i < _group.MembersCount; i++)
            {
                var member = _group.Member(i);
                if (member != null)
                    OnTeammateAdded(member);
            }
        }

        private void OnTeammateAdded(BotOwner teammate)
        {
            if (teammate == null || teammate == _bot || _teammateCaches.ContainsKey(teammate))
                return;

            if (teammate.GetPlayer?.IsAI != true)
                return;

            var cache = teammate.GetComponent<BotComponentCache>();
            if (cache != null)
            {
                _teammateCaches.Add(teammate, cache);
            }
        }

        private void OnTeammateRemoved(BotOwner teammate)
        {
            if (teammate != null)
            {
                _teammateCaches.Remove(teammate);
            }
        }

        #endregion

        #region Update Loop

        private void Update()
        {
            if (_bot == null || _cache == null || _group == null || Time.time < _nextSyncTime)
                return;

            if (_bot.GetPlayer?.IsAI != true || _teammateCaches.Count == 0)
                return;

            _nextSyncTime = Time.time + SyncInterval;
            EvaluateGroupZones();
        }

        private void EvaluateGroupZones()
        {
            string? myZoneId = _cache?.Zone?.ZoneId;
            if (string.IsNullOrEmpty(myZoneId) || myZoneId == "unknown")
                return;

            foreach (var kvp in _teammateCaches)
            {
                var teammate = kvp.Key;
                var teammateCache = kvp.Value;

                if (teammate == null || teammateCache?.Zone == null || teammate.GetPlayer?.IsAI != true)
                    continue;

                string? otherZoneId = teammateCache.Zone.ZoneId;
                if (!string.IsNullOrEmpty(otherZoneId) && otherZoneId != myZoneId)
                {
                    teammateCache.Zone.AssignZone(myZoneId);
                }
            }
        }

        #endregion

        #region Shared Info Distribution

        public void BroadcastLootPoint(Vector3 point) => _lastLootPoint = point;

        public void BroadcastExtractPoint(Vector3 point) => _lastExtractPoint = point;

        public Vector3? GetSharedLootTarget() => _lastLootPoint;

        public Vector3? GetSharedExtractTarget() => _lastExtractPoint;

        public List<BotOwner> GetTeammates()
        {
            _tempList.Clear();
            foreach (var kvp in _teammateCaches)
            {
                var teammate = kvp.Key;
                if (teammate != null && teammate.GetPlayer?.IsAI == true)
                    _tempList.Add(teammate);
            }
            return new List<BotOwner>(_tempList);
        }

        #endregion
    }
}
