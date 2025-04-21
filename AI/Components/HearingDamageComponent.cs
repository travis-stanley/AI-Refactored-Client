#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Components
{
    /// <summary>
    /// Simulates temporary hearing loss in bots after exposure to loud sounds (e.g. gunfire, flashbangs).
    /// Used to reduce detection performance by dampening sound sensitivity.
    /// </summary>
    public class HearingDamageComponent
    {
        #region Fields

        private float _deafness = 0f;
        private float _deafUntilTime = 0f;

        private const float RecoveryRate = 0.3f;        // Deafness decay per second
        private const float DeafenedThreshold = 0.2f;   // Threshold for being "effectively deaf"

        #endregion

        #region Public Properties

        /// <summary>
        /// Current deafness level (0.0 = perfect hearing, 1.0 = fully deaf).
        /// </summary>
        public float Deafness => _deafness;

        /// <summary>
        /// Whether the bot is currently deafened enough to affect hearing.
        /// </summary>
        public bool IsDeafened => _deafness >= DeafenedThreshold;

        /// <summary>
        /// A multiplier (0.0–1.0) that scales how well sounds are perceived.
        /// 1.0 = full mute, 0.0 = normal hearing.
        /// </summary>
        public float VolumeModifier => Mathf.Clamp01(1f - _deafness);

        /// <summary>
        /// Time in seconds before full recovery from hearing loss.
        /// </summary>
        public float RemainingTime => Mathf.Max(0f, _deafUntilTime - Time.time);

        #endregion

        #region Public API

        /// <summary>
        /// Applies hearing damage to the bot. Deafness level will not decrease if higher intensity is reapplied.
        /// Duration stacks to ensure damage cannot prematurely wear off.
        /// </summary>
        /// <param name="intensity">Intensity of deafness (0.0 to 1.0).</param>
        /// <param name="duration">Time in seconds that deafness should persist.</param>
        public void ApplyDeafness(float intensity, float duration)
        {
            intensity = Mathf.Clamp01(intensity);
            _deafness = Mathf.Max(_deafness, intensity);
            _deafUntilTime = Mathf.Max(_deafUntilTime, Time.time + duration);
        }

        /// <summary>
        /// Called once per frame to gradually reduce deafness if its timer has expired.
        /// </summary>
        /// <param name="deltaTime">Time elapsed since last tick.</param>
        public void Tick(float deltaTime)
        {
            if (Time.time >= _deafUntilTime)
            {
                _deafness = Mathf.MoveTowards(_deafness, 0f, RecoveryRate * deltaTime);
            }
        }

        /// <summary>
        /// Instantly clears all hearing loss effects.
        /// </summary>
        public void Clear()
        {
            _deafness = 0f;
            _deafUntilTime = 0f;
        }

        #endregion
    }
}
