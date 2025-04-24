#nullable enable

using AIRefactored.AI.Core;
using EFT;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Coordinates squad-level signal sharing (loot targets, fallback points, danger events).
    /// Syncs between group members using staggered, randomized intervals.
    /// </summary>
    public sealed class BotGroupSyncCoordinator
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotsGroup? _group;

        private readonly Dictionary<BotOwner, BotComponentCache> _teammateCaches = new Dictionary<BotOwner, BotComponentCache>(8);

        private float _nextSyncTime;
        private const float BaseSyncInterval = 0.5f;
        private const float PositionEpsilon = 0.15f;

        private Vector3? _lootPoint;
        private Vector3? _extractPoint;
        private Vector3? _fallbackPoint;

        private float _lastDangerTime = -999f;
        private Vector3 _lastDangerPos;

        private static readonly List<BotOwner> _tempTeammates = new List<BotOwner>(8);

        #endregion

        #region Initialization

        public void Initialize(BotOwner bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _group = bot.BotsGroup;

            if (bot.GetPlayer?.IsAI != true || _group == null)
                return;

            _group.OnMemberAdd += OnMemberAdded;
            _group.OnMemberRemove += OnMemberRemoved;
        }

        private void OnMemberAdded(BotOwner teammate)
        {
            // Optional future hook
        }

        private void OnMemberRemoved(BotOwner teammate)
        {
            _teammateCaches.Remove(teammate);
        }

        #endregion

        #region Cache Injection

        public void InjectLocalCache(BotComponentCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public void InjectTeammateCache(BotOwner owner, BotComponentCache cache)
        {
            if (owner == null || cache == null || _teammateCaches.ContainsKey(owner))
                return;

            _teammateCaches[owner] = cache;
        }

        #endregion

        #region Tick Logic

        public void Tick(float time)
        {
            if (_cache?.Bot?.GetPlayer?.IsAI != true || _teammateCaches.Count == 0)
                return;

            if (time < _nextSyncTime)
                return;

            _nextSyncTime = time + BaseSyncInterval * UnityEngine.Random.Range(0.8f, 1.2f);

            if (_cache.PanicHandler?.IsPanicking == true && _bot != null)
            {
                Vector3 myPos = _bot.Position;

                if (!_fallbackPoint.HasValue || Vector3.SqrMagnitude(_fallbackPoint.Value - myPos) > PositionEpsilon * PositionEpsilon)
                {
                    BroadcastFallbackPoint(myPos);
                }

                if (Vector3.SqrMagnitude(_lastDangerPos - myPos) > PositionEpsilon * PositionEpsilon)
                {
                    BroadcastDanger(myPos);
                }
            }
        }

        #endregion

        #region Broadcasts

        public void BroadcastLootPoint(Vector3 point)
        {
            _lootPoint = point;
        }

        public void BroadcastExtractPoint(Vector3 point)
        {
            _extractPoint = point;
        }

        public void BroadcastFallbackPoint(Vector3 point)
        {
            _fallbackPoint = point;

            foreach (var teammate in _teammateCaches.Values)
            {
                teammate.Combat?.TriggerFallback(point);

                if (teammate.PanicHandler?.IsPanicking != true)
                    teammate.PanicHandler?.TriggerPanic();
            }
        }

        /// <summary>
        /// Broadcasts a danger signal to nearby teammates, triggering panic with a slight delay.
        /// Bots already panicking are skipped.
        /// </summary>
        /// <param name="position">The danger source position.</param>
        public void BroadcastDanger(Vector3 position)
        {
            _lastDangerTime = Time.time;
            _lastDangerPos = position;

            foreach (var entry in _teammateCaches)
            {
                var cache = entry.Value;
                if (cache == null)
                    continue;

                var panicHandler = cache.PanicHandler;
                if (panicHandler != null && !panicHandler.IsPanicking)
                {
                    float delay = UnityEngine.Random.Range(0.1f, 0.4f);
                    FireDelayedPanic(cache, delay);
                }
            }
        }

        private static void FireDelayedPanic(BotComponentCache cache, float delay)
        {
            Task.Run(async () =>
            {
                await Task.Delay((int)(delay * 1000f));
                if (cache?.Bot?.IsDead != false) return;
                cache.PanicHandler?.TriggerPanic();
            });
        }

        #endregion

        #region Queries

        public Vector3? GetSharedLootTarget() => _lootPoint;
        public Vector3? GetSharedExtractTarget() => _extractPoint;
        public Vector3? GetSharedFallbackTarget() => _fallbackPoint;

        public float LastDangerBroadcastTime => _lastDangerTime;
        public Vector3 LastDangerPosition => _lastDangerPos;

        #endregion

        #region Squad Access

        public IReadOnlyList<BotOwner> GetTeammates()
        {
            _tempTeammates.Clear();

            foreach (var kv in _teammateCaches)
            {
                var teammate = kv.Key;
                if (teammate != null && !teammate.IsDead && teammate.GetPlayer?.IsAI == true)
                {
                    _tempTeammates.Add(teammate);
                }
            }

            return _tempTeammates;
        }

        public BotComponentCache? GetCache(BotOwner teammate)
        {
            return _teammateCaches.TryGetValue(teammate, out var cache) ? cache : null;
        }

        public bool IsSquadReady()
        {
            return _bot != null && _group != null && _teammateCaches.Count > 0;
        }

        #endregion

        #region Debug

        public void PrintSquadState()
        {
            Debug.Log($"[GroupSync] Bot: {_bot?.Profile?.Info?.Nickname ?? "Unknown"}, SquadSize: {_teammateCaches.Count}, Fallback: {_fallbackPoint}, Loot: {_lootPoint}");
        }

        #endregion
    }
}
