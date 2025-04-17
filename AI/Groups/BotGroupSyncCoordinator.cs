#nullable enable

using System.Collections.Generic;
using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Coordinates group awareness and information sharing between AI squad members.
    /// Syncs loot targets, extraction targets, and tracks teammate state for cohesion behavior.
    /// </summary>
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

        /// <summary>
        /// Call manually if not using Start() hook to initialize group tracking.
        /// </summary>
        public void Init(BotOwner bot)
        {
            _bot = bot;
            _cache = _bot?.GetComponent<BotComponentCache>();
            _group = _bot?.BotsGroup;

            if (_bot?.GetPlayer?.IsAI != true)
                return;

            RegisterGroupHooks();
        }

        /// <summary>
        /// Unity Start hook — fallback to initialize group tracking.
        /// </summary>
        private void Start()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _group = _bot?.BotsGroup;

            if (_bot?.GetPlayer?.IsAI != true)
                return;

            RegisterGroupHooks();
        }

        /// <summary>
        /// Cleans up listeners on destruction.
        /// </summary>
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

        /// <summary>
        /// Subscribes to group member events and caches current teammates.
        /// </summary>
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

        /// <summary>
        /// Periodic update that may sync zones or group states.
        /// </summary>
        private void Update()
        {
            if (_bot == null || _cache == null || _group == null || Time.time < _nextSyncTime)
                return;

            if (_bot.GetPlayer?.IsAI != true || _teammateCaches.Count == 0)
                return;

            _nextSyncTime = Time.time + SyncInterval;
            EvaluateGroupZones();
        }

        /// <summary>
        /// Stub: Evaluate and synchronize squad-level tactical zones.
        /// </summary>
        private void EvaluateGroupZones()
        {
            // Placeholder for future ZoneID or hotspot target syncing
        }

        #endregion

        #region Shared Info Distribution

        /// <summary>
        /// Shares a loot target with squadmates.
        /// </summary>
        public void BroadcastLootPoint(Vector3 point) => _lastLootPoint = point;

        /// <summary>
        /// Shares a fallback/extraction target with squadmates.
        /// </summary>
        public void BroadcastExtractPoint(Vector3 point) => _lastExtractPoint = point;

        /// <summary>
        /// Retrieves last shared loot position (if any).
        /// </summary>
        public Vector3? GetSharedLootTarget() => _lastLootPoint;

        /// <summary>
        /// Retrieves last shared extract/fallback position (if any).
        /// </summary>
        public Vector3? GetSharedExtractTarget() => _lastExtractPoint;

        /// <summary>
        /// Gets current AI teammates in this bot's squad.
        /// </summary>
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
