#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Tracks who the bot last shot and who last shot them.
    /// Enables memory-based targeting, suppression, and fallback logic.
    /// </summary>
    public class BotLastShotTracker
    {
        #region Fields

        private string? _lastTargetId;
        private float _lastShotTime;

        private string? _lastAttackerId;
        private float _lastHitTime;

        private const float MemoryWindow = 10f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Public API

        /// <summary>
        /// Registers a shot fired by this bot toward a target.
        /// </summary>
        /// <param name="target">The player that was targeted.</param>
        public void RegisterShot(IPlayer? target)
        {
            if (target?.ProfileId == null)
                return;

            _lastTargetId = target.ProfileId;
            _lastShotTime = Time.time;

            // Logger.LogDebug($"[ShotTracker] Fired at {target.ProfileId}");
        }

        /// <summary>
        /// Registers a hit received by this bot from an attacker.
        /// </summary>
        /// <param name="attacker">The player who hit the bot.</param>
        public void RegisterHitBy(IPlayer? attacker)
        {
            if (attacker?.ProfileId == null)
                return;

            _lastAttackerId = attacker.ProfileId;
            _lastHitTime = Time.time;

            // Logger.LogDebug($"[ShotTracker] Hit by {attacker.ProfileId}");
        }

        /// <summary>
        /// Returns true if the bot was recently hit by the specified player.
        /// </summary>
        /// <param name="profileId">The attacker's profile ID.</param>
        /// <param name="now">Optional override of current time (useful for testing).</param>
        /// <returns>True if attacker is recent.</returns>
        public bool WasRecentlyShotBy(string profileId, float now = -1f)
        {
            if (_lastAttackerId != profileId)
                return false;

            if (now < 0f)
                now = Time.time;

            return (now - _lastHitTime) <= MemoryWindow;
        }

        /// <summary>
        /// Returns true if the bot recently shot at the specified player.
        /// </summary>
        /// <param name="profileId">The target's profile ID.</param>
        /// <param name="now">Optional override of current time (useful for testing).</param>
        /// <returns>True if target is recent.</returns>
        public bool DidRecentlyShoot(string profileId, float now = -1f)
        {
            if (_lastTargetId != profileId)
                return false;

            if (now < 0f)
                now = Time.time;

            return (now - _lastShotTime) <= MemoryWindow;
        }

        /// <summary>
        /// Clears all memory of recent shots and hits.
        /// </summary>
        public void Reset()
        {
            _lastTargetId = null;
            _lastAttackerId = null;
            _lastShotTime = 0f;
            _lastHitTime = 0f;

            // Logger.LogDebug("[ShotTracker] Memory reset.");
        }

        #endregion
    }
}
