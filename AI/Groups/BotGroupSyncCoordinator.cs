#nullable enable

using AIRefactored.AI.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Coordinates tactical group state between AIRefactored squadmates.
    /// Handles fallback, extraction, loot, danger syncing, and real-time squad cohesion logic.
    /// </summary>
    public class BotGroupSyncCoordinator
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
        private Vector3? _lastFallbackPoint;

        private float _lastDangerBroadcastTime = -999f;
        private Vector3 _lastDangerPosition = Vector3.zero;

        private static readonly List<BotOwner> _tempList = new();

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the group sync coordinator and binds group callbacks.
        /// </summary>
        public void Initialize(BotOwner bot)
        {
            _bot = bot;
            _cache = bot.GetComponent<BotComponentCache>();
            _group = bot.BotsGroup;

            if (_bot.GetPlayer?.IsAI != true || _group == null)
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

            if (teammate.GetPlayer?.IsAI == true)
            {
                var cache = teammate.GetComponent<BotComponentCache>();
                if (cache != null)
                    _teammateCaches.Add(teammate, cache);
            }
        }

        private void OnTeammateRemoved(BotOwner teammate)
        {
            if (teammate != null)
                _teammateCaches.Remove(teammate);
        }

        #endregion

        #region Tick Logic

        /// <summary>
        /// Periodic logic that will allow for tactical sync like fallback, med, and suppress coordination.
        /// </summary>
        public void Tick(float time)
        {
            if (_bot == null || _cache == null || _group == null || time < _nextSyncTime)
                return;

            if (_bot.GetPlayer?.IsAI != true || _teammateCaches.Count == 0)
                return;

            _nextSyncTime = time + SyncInterval;

            // Shared fallback sync
            if (_cache.PanicHandler?.IsPanicking == true)
            {
                BroadcastFallbackPoint(_bot.Position);
                BroadcastDanger(_bot.Position);
            }
        }

        #endregion

        #region Tactical Broadcast API

        /// <summary>
        /// Shares a squad-level loot point of interest.
        /// </summary>
        public void BroadcastLootPoint(Vector3 point) => _lastLootPoint = point;

        /// <summary>
        /// Shares an extraction target for squad pathing purposes.
        /// </summary>
        public void BroadcastExtractPoint(Vector3 point) => _lastExtractPoint = point;

        /// <summary>
        /// Shares a fallback location, useful during retreats or group panic.
        /// </summary>
        public void BroadcastFallbackPoint(Vector3 point) => _lastFallbackPoint = point;

        /// <summary>
        /// Gets the last shared squad loot point.
        /// </summary>
        public Vector3? GetSharedLootTarget() => _lastLootPoint;

        /// <summary>
        /// Gets the last shared squad extraction point.
        /// </summary>
        public Vector3? GetSharedExtractTarget() => _lastExtractPoint;

        /// <summary>
        /// Gets the last shared fallback position.
        /// </summary>
        public Vector3? GetSharedFallbackTarget() => _lastFallbackPoint;

        /// <summary>
        /// Broadcasts a known danger zone to all squadmates (triggers fallback potential).
        /// </summary>
        public void BroadcastDanger(Vector3 position)
        {
            _lastDangerBroadcastTime = Time.time;
            _lastDangerPosition = position;

            foreach (var cache in _teammateCaches.Values)
            {
                if (cache != null && cache.PanicHandler != null && !cache.Bot?.IsDead == true)
                {
                    cache.PanicHandler.TriggerPanic();
                }
            }
        }

        /// <summary>
        /// Gets the last shared danger broadcast time.
        /// </summary>
        public float LastDangerBroadcastTime => _lastDangerBroadcastTime;

        /// <summary>
        /// Gets the last known danger broadcast location.
        /// </summary>
        public Vector3 LastDangerPosition => _lastDangerPosition;

        #endregion

        #region Teammate Utilities

        /// <summary>
        /// Returns all active, AI squadmates in the current group.
        /// </summary>
        public List<BotOwner> GetTeammates()
        {
            _tempList.Clear();

            foreach (var kvp in _teammateCaches)
            {
                var teammate = kvp.Key;
                if (teammate != null && teammate.GetPlayer?.IsAI == true && !teammate.IsDead)
                    _tempList.Add(teammate);
            }

            return new List<BotOwner>(_tempList);
        }

        /// <summary>
        /// Gets the component cache for a specific squadmate, if available.
        /// </summary>
        public BotComponentCache? GetCache(BotOwner teammate)
        {
            if (teammate != null && _teammateCaches.TryGetValue(teammate, out var cache))
                return cache;

            return null;
        }

        /// <summary>
        /// Checks if the bot is part of a valid synchronized squad.
        /// </summary>
        public bool IsSquadReady()
        {
            return _bot != null && _group != null && _teammateCaches.Count > 0;
        }

        #endregion
    }
}
