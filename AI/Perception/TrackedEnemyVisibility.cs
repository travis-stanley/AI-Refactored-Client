#nullable enable

namespace AIRefactored.AI.Perception
{
    using System.Collections.Generic;

    using UnityEngine;

    /// <summary>
    ///     Tracks enemy bone visibility from the bot's perspective.
    ///     Simulates partial body exposure, confidence-based decisions, and occlusion over time.
    /// </summary>
    public sealed class TrackedEnemyVisibility
    {
        private const float BoneVisibilityDuration = 0.5f;

        private const float LinecastSlack = 0.15f;

        private static readonly Queue<string> ExpiredKeys = new(8);

        private readonly Transform _botOrigin;

        private readonly Dictionary<string, BoneInfo> _visibleBones = new(8);

        public TrackedEnemyVisibility(Transform botOrigin)
        {
            this._botOrigin = botOrigin;
        }

        /// <summary>
        ///     Returns true if enough body parts are visible to make a confident decision.
        /// </summary>
        public bool HasEnoughData => this._visibleBones.Count >= 2;

        /// <summary>
        ///     Returns true if any bones are visible and unexpired.
        /// </summary>
        public bool CanSeeAny()
        {
            this.CleanExpired(Time.time);
            return this._visibleBones.Count > 0;
        }

        /// <summary>
        ///     Checks whether a previously seen bone is still shootable (unobstructed).
        /// </summary>
        public bool CanShootTo(string boneName)
        {
            if (!this._visibleBones.TryGetValue(boneName, out var info))
                return false;

            var now = Time.time;
            if (now - info.Timestamp > BoneVisibilityDuration)
                return false;

            var eye = this._botOrigin.position + Vector3.up * 1.4f;
            var dist = Vector3.Distance(eye, info.Position);

            return !Physics.Linecast(eye, info.Position, out var hit) || hit.distance >= dist - LinecastSlack;
        }

        /// <summary>
        ///     Clears all tracked visibility data.
        /// </summary>
        public void Clear()
        {
            this._visibleBones.Clear();
        }

        /// <summary>
        ///     Simulates memory decay by aging out timestamps.
        /// </summary>
        public void DecayConfidence(float decayAmount)
        {
            var now = Time.time;

            foreach (var key in this._visibleBones.Keys)
            {
                var info = this._visibleBones[key];
                var newTimestamp = Mathf.Max(0f, info.Timestamp - decayAmount);
                this._visibleBones[key] = new BoneInfo(info.Position, newTimestamp);
            }

            this.CleanExpired(now);
        }

        /// <summary>
        ///     Number of tracked visible bones.
        /// </summary>
        public int ExposedBoneCount()
        {
            this.CleanExpired(Time.time);
            return this._visibleBones.Count;
        }

        /// <summary>
        ///     Confidence score from 0.0 to 1.0 based on bone exposure.
        /// </summary>
        public float GetOverallConfidence()
        {
            this.CleanExpired(Time.time);
            return Mathf.Clamp01(this._visibleBones.Count / 8f);
        }

        /// <summary>
        ///     Updates visibility tracking for a specific bone.
        /// </summary>
        public void UpdateBoneVisibility(string boneName, Vector3 worldPosition)
        {
            this._visibleBones[boneName] = new BoneInfo(worldPosition, Time.time);
        }

        private void CleanExpired(float now)
        {
            ExpiredKeys.Clear();

            foreach (var kvp in this._visibleBones)
                if (now - kvp.Value.Timestamp > BoneVisibilityDuration)
                    ExpiredKeys.Enqueue(kvp.Key);

            while (ExpiredKeys.Count > 0) this._visibleBones.Remove(ExpiredKeys.Dequeue());
        }

        private readonly struct BoneInfo
        {
            public Vector3 Position { get; }

            public float Timestamp { get; }

            public BoneInfo(Vector3 position, float timestamp)
            {
                this.Position = position;
                this.Timestamp = timestamp;
            }
        }
    }
}