#nullable enable

using AIRefactored.Runtime;
using BepInEx.Logging;
using UnityEngine;

namespace AIRefactored.AI.Components
{
    /// <summary>
    /// Simulates temporary hearing loss in bots after exposure to loud sounds (e.g., gunfire, flashbangs).
    /// Deafness reduces detection of auditory cues and gradually recovers over time.
    /// </summary>
    public sealed class HearingDamageComponent
    {
        #region Fields

        private float _deafness;
        private float _deafUntilTime;

        private const float RecoveryRate = 0.3f;
        private const float DeafenedThreshold = 0.2f;
        private const float FadeInSpeed = 3.5f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Properties

        /// <summary>
        /// Current deafness level (0.0 = perfect hearing, 1.0 = fully deaf).
        /// </summary>
        public float Deafness => _deafness;

        /// <summary>
        /// Whether the bot is currently deafened enough to impair hearing.
        /// </summary>
        public bool IsDeafened => _deafness >= DeafenedThreshold;

        /// <summary>
        /// Volume multiplier (0.0 = fully muted, 1.0 = perfect hearing).
        /// </summary>
        public float VolumeModifier => Mathf.Clamp01(1f - _deafness);

        /// <summary>
        /// Time in seconds remaining before hearing begins to recover.
        /// </summary>
        public float RemainingTime => Mathf.Max(0f, _deafUntilTime - Time.time);

        #endregion

        #region Public API

        /// <summary>
        /// Applies hearing damage to the bot.
        /// Deafness is raised if intensity exceeds current, and recovery is delayed if duration extends the timer.
        /// </summary>
        /// <param name="newLevel">Deafness severity (0.0 to 1.0).</param>
        /// <param name="recoveryDelay">Delay before recovery begins (in seconds).</param>
        public void ApplyDeafness(float newLevel, float recoveryDelay)
        {
            newLevel = Mathf.Clamp01(newLevel);

            if (newLevel > _deafness)
            {
                Logger.LogDebug($"[Hearing] Deafness increased: {_deafness:F2} → {newLevel:F2} (delay: {recoveryDelay:F2}s)");
                // Smooth onset instead of snapping for realism
                _deafness = Mathf.MoveTowards(_deafness, newLevel, FadeInSpeed * Time.deltaTime);
            }

            float newExpiry = Time.time + recoveryDelay;
            if (newExpiry > _deafUntilTime)
                _deafUntilTime = newExpiry;
        }

        /// <summary>
        /// Reduces deafness over time after recovery delay expires.
        /// </summary>
        /// <param name="deltaTime">Elapsed time since last tick.</param>
        public void Tick(float deltaTime)
        {
            if (Time.time < _deafUntilTime)
                return;

            if (_deafness <= 0f)
                return;

            float previous = _deafness;
            _deafness = Mathf.MoveTowards(_deafness, 0f, RecoveryRate * deltaTime);

            if (previous > DeafenedThreshold && _deafness <= DeafenedThreshold)
            {
                Logger.LogDebug("[Hearing] Deafness recovered below perceptual threshold.");
            }
        }

        /// <summary>
        /// Instantly removes all hearing impairment.
        /// </summary>
        public void Clear()
        {
            if (_deafness > 0f)
                Logger.LogDebug($"[Hearing] Clearing deafness (was {_deafness:F2}).");

            _deafness = 0f;
            _deafUntilTime = 0f;
        }

        #endregion
    }
}
