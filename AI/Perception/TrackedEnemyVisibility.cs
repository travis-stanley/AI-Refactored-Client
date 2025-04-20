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
        private readonly Transform? _botOrigin;

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
        public bool CanSeeAny(float now = -1f)
        {
            if (now < 0f)
                now = Time.time;

            List<string> toRemove = null!;
            bool seen = false;

            foreach (var kvp in _visibleBones)
            {
                if (now - kvp.Value.LastSeenTime <= VisibilityTimeout)
                {
                    seen = true;
                }
                else
                {
                    if (toRemove == null)
                        toRemove = new List<string>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                    _visibleBones.Remove(toRemove[i]);
            }

            return seen;
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

            if (_botOrigin == null)
                return false;

            Vector3 origin = _botOrigin.position + Vector3.up * 1.4f;
            Vector3 target = info.WorldPosition;
            float maxDist = Vector3.Distance(origin, target);

            if (Physics.Linecast(origin, target, out var hit))
            {
                return hit.collider != null && hit.distance >= maxDist - 0.15f;
            }

            return true;
        }

        /// <summary>
        /// Returns the total number of bones currently exposed.
        /// </summary>
        public int ExposedBoneCount(float now = -1f)
        {
            if (now < 0f)
                now = Time.time;

            int count = 0;
            List<string> toRemove = null!;

            foreach (var kvp in _visibleBones)
            {
                if (now - kvp.Value.LastSeenTime <= VisibilityTimeout)
                {
                    count++;
                }
                else
                {
                    if (toRemove == null)
                        toRemove = new List<string>();
                    toRemove.Add(kvp.Key);
                }
            }

            if (toRemove != null)
            {
                for (int i = 0; i < toRemove.Count; i++)
                    _visibleBones.Remove(toRemove[i]);
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
