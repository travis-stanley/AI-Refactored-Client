#nullable enable

using System.Collections.Generic;
using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Synchronizes fallback zones, loot targets, and extract coordination among group members.
    /// </summary>
    public class BotGroupSyncCoordinator : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotsGroup? _group;

        private readonly Dictionary<BotOwner, BotComponentCache> _teammateCaches = new();
        private float _nextSyncTime = 0f;
        private const float SyncInterval = 0.5f;

        private Vector3? _lastLootPoint;
        private Vector3? _lastExtractPoint;

        public void Init(BotOwner bot)
        {
            _bot = bot;
            _cache = _bot?.GetComponent<BotComponentCache>();
            _group = _bot?.BotsGroup;

            RegisterGroupHooks();
        }

        private void Start()
        {
            if (_bot == null)
            {
                _bot = GetComponent<BotOwner>();
                _cache = GetComponent<BotComponentCache>();
                _group = _bot?.BotsGroup;
            }

            RegisterGroupHooks();
        }

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

        private void OnDestroy()
        {
            if (_group != null)
            {
                _group.OnMemberAdd -= OnTeammateAdded;
                _group.OnMemberRemove -= OnTeammateRemoved;
            }
        }

        private void OnTeammateAdded(BotOwner teammate)
        {
            if (teammate == null || teammate == _bot || _teammateCaches.ContainsKey(teammate))
                return;

            var cache = teammate.GetComponent<BotComponentCache>();
            if (cache != null)
            {
                _teammateCaches[teammate] = cache;
            }
        }

        private void OnTeammateRemoved(BotOwner teammate)
        {
            if (teammate != null)
            {
                _teammateCaches.Remove(teammate);
            }
        }

        private void Update()
        {
            if (_bot == null || _cache == null || _group == null || Time.time < _nextSyncTime)
                return;

            _nextSyncTime = Time.time + SyncInterval;
            EvaluateGroupZones();
        }

        private void EvaluateGroupZones()
        {
            if (_teammateCaches.Count == 0)
                return;

            string myZoneId = _cache.Zone?.ZoneId ?? "unknown";

            foreach (var kvp in _teammateCaches)
            {
                var teammate = kvp.Key;
                var teammateCache = kvp.Value;

                if (teammate == null || teammateCache?.Zone == null)
                    continue;

                string otherZoneId = teammateCache.Zone.ZoneId ?? "unknown";

                if (otherZoneId != myZoneId && myZoneId != "unknown")
                {
                    teammateCache.Zone.AssignZone(myZoneId);

                }
            }
        }

        // === Shared Info ===

        public void BroadcastLootPoint(Vector3 point)
        {
            _lastLootPoint = point;
        }

        public void BroadcastExtractPoint(Vector3 point)
        {
            _lastExtractPoint = point;
        }

        public Vector3? GetSharedLootTarget()
        {
            return _lastLootPoint;
        }

        public Vector3? GetSharedExtractTarget()
        {
            return _lastExtractPoint;
        }

        /// <summary>
        /// Returns a list of synced teammates.
        /// </summary>
        public List<BotOwner> GetTeammates()
        {
            var list = new List<BotOwner>(_teammateCaches.Count);
            foreach (var teammate in _teammateCaches.Keys)
            {
                if (teammate != null)
                    list.Add(teammate);
            }
            return list;
        }
    }
}
