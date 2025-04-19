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
        public void RegisterShot(IPlayer enemy)
        {
            if (enemy?.ProfileId != null)
            {
                _lastTargetProfileId = enemy.ProfileId;
                _lastShotTime = Time.time;
            }
        }

        /// <summary>
        /// Call when the bot is hit by an attacker.
        /// </summary>
        public void RegisterHitBy(IPlayer attacker)
        {
            if (attacker?.ProfileId != null)
            {
                _lastHitByProfileId = attacker.ProfileId;
                _lastHitTime = Time.time;
            }
        }

        /// <summary>
        /// Returns true if the bot was recently hit by the given profile.
        /// </summary>
        public bool WasRecentlyShotBy(string profileId)
        {
            return _lastHitByProfileId == profileId && (Time.time - _lastHitTime) <= MemoryDuration;
        }

        /// <summary>
        /// Returns true if the bot recently shot at the given profile.
        /// </summary>
        public bool DidRecentlyShoot(string profileId)
        {
            return _lastTargetProfileId == profileId && (Time.time - _lastShotTime) <= MemoryDuration;
        }

        /// <summary>
        /// Clears all memory of shot/hit history.
        /// </summary>
        public void Reset()
        {
            _lastTargetProfileId = null;
            _lastHitByProfileId = null;
        }
    }
}
