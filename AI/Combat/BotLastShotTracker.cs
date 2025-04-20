#nullable enable

using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Tracks who the bot last shot, and who last shot them.
    /// Enables memory-based retaliation, suppression, and fallback logic.
    /// </summary>
    public class BotLastShotTracker
    {
        private string? _lastTargetProfileId;
        private float _lastShotTime;

        private string? _lastHitByProfileId;
        private float _lastHitTime;

        private const float MemoryDuration = 10f;

        /// <summary>
        /// Call when the bot fires at a target.
        /// </summary>
        public void RegisterShot(IPlayer? enemy)
        {
            if (enemy == null || string.IsNullOrEmpty(enemy.ProfileId))
                return;

            _lastTargetProfileId = enemy.ProfileId;
            _lastShotTime = Time.time;
        }

        /// <summary>
        /// Call when the bot is hit by an attacker.
        /// </summary>
        public void RegisterHitBy(IPlayer? attacker)
        {
            if (attacker == null || string.IsNullOrEmpty(attacker.ProfileId))
                return;

            _lastHitByProfileId = attacker.ProfileId;
            _lastHitTime = Time.time;
        }

        /// <summary>
        /// Returns true if the bot was recently hit by the given profile.
        /// </summary>
        public bool WasRecentlyShotBy(string profileId, float now = -1f)
        {
            if (string.IsNullOrEmpty(profileId) || _lastHitByProfileId != profileId)
                return false;

            if (now < 0f) now = Time.time;
            return (now - _lastHitTime) <= MemoryDuration;
        }

        /// <summary>
        /// Returns true if the bot recently shot at the given profile.
        /// </summary>
        public bool DidRecentlyShoot(string profileId, float now = -1f)
        {
            if (string.IsNullOrEmpty(profileId) || _lastTargetProfileId != profileId)
                return false;

            if (now < 0f) now = Time.time;
            return (now - _lastShotTime) <= MemoryDuration;
        }

        /// <summary>
        /// Clears all memory of shot/hit history.
        /// </summary>
        public void Reset()
        {
            _lastTargetProfileId = null;
            _lastHitByProfileId = null;
            _lastShotTime = 0f;
            _lastHitTime = 0f;
        }
    }
}
