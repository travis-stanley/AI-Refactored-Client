#nullable enable

namespace AIRefactored.AI.Memory
{
    using System;
    using System.Collections.Generic;

    using AIRefactored.AI.Core;
    using AIRefactored.Core;

    using UnityEngine;

    /// <summary>
    ///     Stores recent enemy sightings and cleared safe zones.
    ///     Helps bots avoid repeat danger and synchronize fallback intel.
    /// </summary>
    public class BotTacticalMemory
    {
        private const float ClearedMemoryDuration = 10f;

        private const float GridSnapSize = 0.5f;

        private const float MaxMemoryTime = 14f;

        private const float PositionToleranceSqr = 0.25f;

        private readonly Dictionary<Vector3, float> _clearedSpots = new(32, new Vector3EqualityComparer());

        private readonly Dictionary<string, SeenEnemyRecord> _enemyMemoryById = new(
            4,
            StringComparer.OrdinalIgnoreCase);

        private readonly List<SeenEnemyRecord> _enemyMemoryList = new(4);

        private BotComponentCache? _cache;

        public void CullExpired()
        {
            var now = Time.time;
            this._enemyMemoryList.RemoveAll((SeenEnemyRecord mem) => now - mem.TimeSeen > MaxMemoryTime);

            List<string> toRemove = new();
            foreach (var kvp in this._enemyMemoryById)
                if (now - kvp.Value.TimeSeen > MaxMemoryTime)
                    toRemove.Add(kvp.Key);

            foreach (var key in toRemove) this._enemyMemoryById.Remove(key);
        }

        public List<SeenEnemyRecord> GetAllMemory()
        {
            return this._enemyMemoryList;
        }

        public Vector3? GetRecentEnemyMemory()
        {
            var now = Time.time;
            SeenEnemyRecord? freshest = null;

            for (var i = 0; i < this._enemyMemoryList.Count; i++)
            {
                var mem = this._enemyMemoryList[i];
                if (now - mem.TimeSeen <= MaxMemoryTime)
                    if (!freshest.HasValue || mem.TimeSeen > freshest.Value.TimeSeen)
                        freshest = mem;
            }

            return freshest?.Position;
        }

        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache;
        }

        public bool IsZoneUnsafe(Vector3 position)
        {
            if (this._cache?.Bot == null)
                return false;

            var map = GameWorldHandler.GetCurrentMapName();
            var gridPos = SnapToGrid(position);
            var now = Time.time;

            for (var i = 0; i < this._enemyMemoryList.Count; i++)
                if ((gridPos - this._enemyMemoryList[i].Position).sqrMagnitude < PositionToleranceSqr)
                    return true;

            foreach (var kvp in this._clearedSpots)
                if ((kvp.Key - gridPos).sqrMagnitude < PositionToleranceSqr && now - kvp.Value < ClearedMemoryDuration)
                    return true;

            return BotMemoryStore.IsPositionInDangerZone(map, position);
        }

        public void MarkCleared(Vector3 position)
        {
            this._clearedSpots[SnapToGrid(position)] = Time.time;
        }

        public void RecordEnemyPosition(Vector3 position, string? tag = "Generic", string? enemyId = null)
        {
            if (this._cache == null || this._cache.IsBlinded || (this._cache.PanicHandler?.IsPanicking ?? false))
                return;

            var now = Time.time;
            var gridPos = SnapToGrid(position);

            // Fully null-safe tag handling
            string finalTag;
            if (tag == null || string.IsNullOrWhiteSpace(tag))
            {
                finalTag = "Generic";
            }
            else
            {
                var trimmed = tag.Trim();
                finalTag = string.IsNullOrEmpty(trimmed) ? "Generic" : trimmed;
            }

            // Fully null-safe enemyId handling
            if (enemyId != null && !string.IsNullOrWhiteSpace(enemyId))
            {
                var trimmedId = enemyId.Trim();
                if (!string.IsNullOrEmpty(trimmedId))
                    this._enemyMemoryById[trimmedId] = new SeenEnemyRecord(gridPos, now, finalTag);
            }

            for (var i = 0; i < this._enemyMemoryList.Count; i++)
                if ((gridPos - this._enemyMemoryList[i].Position).sqrMagnitude < PositionToleranceSqr)
                {
                    this._enemyMemoryList[i] = new SeenEnemyRecord(gridPos, now, finalTag);
                    return;
                }

            this._enemyMemoryList.Add(new SeenEnemyRecord(gridPos, now, finalTag));
        }

        public void ResetMemory()
        {
            this._enemyMemoryList.Clear();
            this._enemyMemoryById.Clear();
            this._clearedSpots.Clear();
        }

        public void ShareMemoryWith(List<BotComponentCache> teammates)
        {
            foreach (var record in this._enemyMemoryList)
            foreach (var mate in teammates)
            {
                if (mate?.Bot == null || mate.Bot == this._cache?.Bot)
                    continue;

                var id = this._cache?.Bot?.Profile?.Id ?? "unknown";
                mate.TacticalMemory?.SyncMemory(record.Position, "Echo:" + id);
            }
        }

        public void SyncMemory(Vector3 position, string tag = "AllyEcho")
        {
            this.RecordEnemyPosition(position, tag);
        }

        public bool WasRecentlyCleared(Vector3 position)
        {
            var gridPos = SnapToGrid(position);
            return this._clearedSpots.TryGetValue(gridPos, out var lastTime)
                   && Time.time - lastTime < ClearedMemoryDuration;
        }

        private static Vector3 SnapToGrid(Vector3 pos)
        {
            return new Vector3(
                Mathf.Round(pos.x / GridSnapSize) * GridSnapSize,
                Mathf.Round(pos.y / GridSnapSize) * GridSnapSize,
                Mathf.Round(pos.z / GridSnapSize) * GridSnapSize);
        }

        public struct SeenEnemyRecord
        {
            public Vector3 Position;

            public float TimeSeen;

            public string Tag;

            public SeenEnemyRecord(Vector3 position, float time, string tag)
            {
                this.Position = position;
                this.TimeSeen = time;
                this.Tag = tag;
            }
        }

        private class Vector3EqualityComparer : IEqualityComparer<Vector3>
        {
            public bool Equals(Vector3 a, Vector3 b)
            {
                return (a - b).sqrMagnitude < PositionToleranceSqr;
            }

            public int GetHashCode(Vector3 v)
            {
                unchecked
                {
                    var hash = 17;
                    hash = hash * 23 + Mathf.RoundToInt(v.x * 10f);
                    hash = hash * 23 + Mathf.RoundToInt(v.y * 10f);
                    hash = hash * 23 + Mathf.RoundToInt(v.z * 10f);
                    return hash;
                }
            }
        }
    }
}