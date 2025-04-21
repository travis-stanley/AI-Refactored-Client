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
        #region Fields

        private readonly Transform? _botOrigin;
        private const float VisibilityTimeout = 0.5f;

        private readonly Dictionary<string, BoneVisibilityInfo> _visibleBones = new Dictionary<string, BoneVisibilityInfo>(8);

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a visibility tracker tied to the bot's vision origin.
        /// </summary>
        /// <param name="botOrigin">Transform of the bot's eye-level or camera root.</param>
        public TrackedEnemyVisibility(Transform botOrigin)
        {
            _botOrigin = botOrigin;
        }

        #endregion

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
        /// Expired entries are removed.
        /// </summary>
        public bool CanSeeAny(float now = -1f)
        {
            if (now < 0f)
                now = Time.time;

            bool seen = false;
            List<string>? expired = null;

            foreach (var kvp in _visibleBones)
            {
                if (now - kvp.Value.LastSeenTime <= VisibilityTimeout)
                {
                    seen = true;
                }
                else
                {
                    expired ??= new List<string>();
                    expired.Add(kvp.Key);
                }
            }

            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                    _visibleBones.Remove(expired[i]);
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
            float dist = Vector3.Distance(origin, target);

            return !Physics.Linecast(origin, target, out RaycastHit hit) ||
                   (hit.collider != null && hit.distance >= dist - 0.15f);
        }

        /// <summary>
        /// Returns the total number of bones currently exposed within the visibility timeout.
        /// Expired entries are removed.
        /// </summary>
        public int ExposedBoneCount(float now = -1f)
        {
            if (now < 0f)
                now = Time.time;

            int count = 0;
            List<string>? expired = null;

            foreach (var kvp in _visibleBones)
            {
                if (now - kvp.Value.LastSeenTime <= VisibilityTimeout)
                {
                    count++;
                }
                else
                {
                    expired ??= new List<string>();
                    expired.Add(kvp.Key);
                }
            }

            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                    _visibleBones.Remove(expired[i]);
            }

            return count;
        }

        /// <summary>
        /// Clears all bone visibility data immediately.
        /// </summary>
        public void Clear()
        {
            _visibleBones.Clear();
        }

        #endregion

        #region Internal Types

        private struct BoneVisibilityInfo
        {
            public Vector3 WorldPosition;
            public float LastSeenTime;
        }

        #endregion
    }
}
