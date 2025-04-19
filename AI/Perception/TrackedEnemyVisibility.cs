#nullable enable

using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Tracks visibility of multiple enemy bones to simulate partial exposure and realistic detection.
    /// Used by bots to assess if any body part is exposed (e.g., shoulder, thigh, spine).
    /// </summary>
    public class TrackedEnemyVisibility
    {
        private readonly Transform _botOrigin;

        private const float VisibilityTimeout = 0.5f;
        private readonly Dictionary<string, BoneVisibilityInfo> _visibleBones = new(8);

        public TrackedEnemyVisibility(Transform botOrigin)
        {
            _botOrigin = botOrigin;
        }

        #region Public API

        /// <summary>
        /// Records visibility of a specific bone at the given world position.
        /// </summary>
        public void UpdateBoneVisibility(string boneName, Vector3 worldPosition)
        {
            _visibleBones[boneName] = new BoneVisibilityInfo
            {
                WorldPosition = worldPosition,
                LastSeenTime = Time.time
            };
        }

        /// <summary>
        /// Returns true if any tracked bone has been seen recently.
        /// </summary>
        public bool CanSeeAny()
        {
            float now = Time.time;
            foreach (var entry in _visibleBones.Values)
            {
                if (now - entry.LastSeenTime <= VisibilityTimeout)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if this bot has a clear shot path to the specified bone.
        /// </summary>
        public bool CanShootTo(string boneName)
        {
            if (!_visibleBones.TryGetValue(boneName, out var info))
                return false;

            if (Time.time - info.LastSeenTime > VisibilityTimeout)
                return false;

            Vector3 origin = _botOrigin.position + Vector3.up * 1.4f;

            if (!Physics.Linecast(origin, info.WorldPosition, out var hit))
                return true;

            return hit.collider == null || hit.distance >= Vector3.Distance(origin, info.WorldPosition) - 0.1f;
        }

        /// <summary>
        /// Returns the total number of bones currently exposed.
        /// </summary>
        public int ExposedBoneCount()
        {
            int count = 0;
            float now = Time.time;

            foreach (var entry in _visibleBones.Values)
            {
                if (now - entry.LastSeenTime <= VisibilityTimeout)
                    count++;
            }

            return count;
        }

        /// <summary>
        /// Clears all memory (e.g., when target dies or bot resets).
        /// </summary>
        public void Clear()
        {
            _visibleBones.Clear();
        }

        #endregion

        #region Internal Struct

        private struct BoneVisibilityInfo
        {
            public Vector3 WorldPosition;
            public float LastSeenTime;
        }

        #endregion
    }
}
