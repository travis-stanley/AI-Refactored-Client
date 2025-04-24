#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Tracks enemy bone visibility from the bot's perspective.
    /// Simulates partial body exposure, confidence-based decisions, and occlusion over time.
    /// </summary>
    public sealed class TrackedEnemyVisibility
    {
        #region Constants

        private const float BoneVisibilityDuration = 0.5f;
        private const float LinecastSlack = 0.15f;

        private static readonly Queue<string> ExpiredKeys = new(8);

        #endregion

        #region Fields

        private readonly Transform _botOrigin;
        private readonly Dictionary<string, BoneInfo> _visibleBones = new(8);

        #endregion

        #region Constructor

        public TrackedEnemyVisibility(Transform botOrigin)
        {
            _botOrigin = botOrigin;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Updates visibility tracking for a specific bone.
        /// </summary>
        public void UpdateBoneVisibility(string boneName, Vector3 worldPosition)
        {
            _visibleBones[boneName] = new BoneInfo(worldPosition, Time.time);
        }

        /// <summary>
        /// Returns true if any bones are visible and unexpired.
        /// </summary>
        public bool CanSeeAny()
        {
            CleanExpired(Time.time);
            return _visibleBones.Count > 0;
        }

        /// <summary>
        /// Checks whether a previously seen bone is still shootable (unobstructed).
        /// </summary>
        public bool CanShootTo(string boneName)
        {
            if (!_visibleBones.TryGetValue(boneName, out var info))
                return false;

            float now = Time.time;
            if (now - info.Timestamp > BoneVisibilityDuration)
                return false;

            Vector3 eye = _botOrigin.position + Vector3.up * 1.4f;
            float dist = Vector3.Distance(eye, info.Position);

            return !Physics.Linecast(eye, info.Position, out var hit) || hit.distance >= dist - LinecastSlack;
        }

        /// <summary>
        /// Number of tracked visible bones.
        /// </summary>
        public int ExposedBoneCount()
        {
            CleanExpired(Time.time);
            return _visibleBones.Count;
        }

        /// <summary>
        /// Confidence score from 0.0 to 1.0 based on bone exposure.
        /// </summary>
        public float GetOverallConfidence()
        {
            CleanExpired(Time.time);
            return Mathf.Clamp01(_visibleBones.Count / 8f);
        }

        /// <summary>
        /// Returns true if enough body parts are visible to make a confident decision.
        /// </summary>
        public bool HasEnoughData => _visibleBones.Count >= 2;

        /// <summary>
        /// Simulates memory decay by aging out timestamps.
        /// </summary>
        public void DecayConfidence(float decayAmount)
        {
            float now = Time.time;

            foreach (var key in _visibleBones.Keys)
            {
                BoneInfo info = _visibleBones[key];
                float newTimestamp = Mathf.Max(0f, info.Timestamp - decayAmount);
                _visibleBones[key] = new BoneInfo(info.Position, newTimestamp);
            }

            CleanExpired(now);
        }

        /// <summary>
        /// Clears all tracked visibility data.
        /// </summary>
        public void Clear()
        {
            _visibleBones.Clear();
        }

        #endregion

        #region Cleanup

        private void CleanExpired(float now)
        {
            ExpiredKeys.Clear();

            foreach (var kvp in _visibleBones)
            {
                if (now - kvp.Value.Timestamp > BoneVisibilityDuration)
                    ExpiredKeys.Enqueue(kvp.Key);
            }

            while (ExpiredKeys.Count > 0)
            {
                _visibleBones.Remove(ExpiredKeys.Dequeue());
            }
        }

        #endregion

        #region Structs

        private readonly struct BoneInfo
        {
            public Vector3 Position { get; }
            public float Timestamp { get; }

            public BoneInfo(Vector3 position, float timestamp)
            {
                Position = position;
                Timestamp = timestamp;
            }
        }

        #endregion
    }
}
