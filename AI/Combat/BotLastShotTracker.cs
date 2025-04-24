#nullable enable

using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Tracks recent shot and hit interactions for a bot.
    /// Used to inform suppression, retaliation, and fallback logic.
    /// </summary>
    public sealed class BotLastShotTracker
    {
        #region Constants

        private const float DefaultMemoryWindow = 10f;

        #endregion

        #region Fields

        private string? _lastTargetId;
        private float _lastShotTime;

        private string? _lastAttackerId;
        private float _lastHitTime;

        #endregion

        #region Shot Tracking

        /// <summary>
        /// Records a shot fired by this bot at a target.
        /// </summary>
        /// <param name="target">The player this bot fired at.</param>
        public void RegisterShot(IPlayer? target)
        {
            if (target == null || string.IsNullOrEmpty(target.ProfileId))
                return;

            _lastTargetId = target.ProfileId;
            _lastShotTime = Time.time;
        }

        /// <summary>
        /// Returns true if the bot shot at this profile recently.
        /// </summary>
        /// <param name="profileId">Target profile ID.</param>
        /// <param name="now">Optional override for current time.</param>
        /// <param name="memoryWindow">Duration to remember the event.</param>
        public bool DidRecentlyShoot(string profileId, float now = -1f, float memoryWindow = DefaultMemoryWindow)
        {
            if (string.IsNullOrEmpty(_lastTargetId) || _lastTargetId != profileId)
                return false;

            float currentTime = now >= 0f ? now : Time.time;
            return (currentTime - _lastShotTime) <= memoryWindow;
        }

        #endregion

        #region Hit Tracking

        /// <summary>
        /// Records that this bot was hit by a player.
        /// </summary>
        /// <param name="attacker">The player who hit this bot.</param>
        public void RegisterHitBy(IPlayer? attacker)
        {
            if (attacker == null || string.IsNullOrEmpty(attacker.ProfileId))
                return;

            _lastAttackerId = attacker.ProfileId;
            _lastHitTime = Time.time;
        }

        /// <summary>
        /// Returns true if the attacker hit this bot within the memory window.
        /// </summary>
        /// <param name="profileId">Attacker profile ID.</param>
        /// <param name="now">Optional override for current time.</param>
        /// <param name="memoryWindow">Duration to remember the event.</param>
        public bool WasRecentlyShotBy(string profileId, float now = -1f, float memoryWindow = DefaultMemoryWindow)
        {
            if (string.IsNullOrEmpty(_lastAttackerId) || _lastAttackerId != profileId)
                return false;

            float currentTime = now >= 0f ? now : Time.time;
            return (currentTime - _lastHitTime) <= memoryWindow;
        }

        #endregion

        #region Maintenance

        /// <summary>
        /// Clears the memory of shots and hits.
        /// </summary>
        public void Reset()
        {
            _lastTargetId = null;
            _lastShotTime = 0f;
            _lastAttackerId = null;
            _lastHitTime = 0f;
        }

        #endregion
    }
}
