#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Components
{
    /// <summary>
    /// Tracks and applies hearing loss effects to a bot after loud sounds (gunfire, explosions, flashbangs).
    /// Used to dampen sound detection and simulate temporary deafness.
    /// </summary>
    public class HearingDamageComponent
    {
        private float _deafness = 0f;
        private float _deafUntilTime = 0f;

        private const float RecoveryRate = 0.3f; // Per second
        private const float DeafenedThreshold = 0.2f;

        /// <summary>
        /// Current deafness level (0.0 = full hearing, 1.0 = fully deaf).
        /// </summary>
        public float Deafness => _deafness;

        /// <summary>
        /// True if deafness is significant enough to impair sound detection.
        /// </summary>
        public bool IsDeafened => _deafness >= DeafenedThreshold;

        /// <summary>
        /// Amount by which sound volume should be reduced (1.0 = no sound, 0.0 = unaffected).
        /// Can be used to scale auditory perception accuracy.
        /// </summary>
        public float VolumeModifier => Mathf.Clamp01(1f - _deafness);

        /// <summary>
        /// Time remaining before deafness completely wears off.
        /// </summary>
        public float RemainingTime => Mathf.Max(0f, _deafUntilTime - Time.time);

        /// <summary>
        /// Applies a deafening effect to the bot. Deafness will be sustained at the highest applied level.
        /// Duration is cumulative and cannot regress early.
        /// </summary>
        /// <param name="intensity">Value between 0.0 and 1.0</param>
        /// <param name="duration">Duration in seconds</param>
        public void ApplyDeafness(float intensity, float duration)
        {
            intensity = Mathf.Clamp01(intensity);
            _deafness = Mathf.Max(_deafness, intensity);
            _deafUntilTime = Mathf.Max(_deafUntilTime, Time.time + duration);
        }

        /// <summary>
        /// Called each frame (from BotBrain or scheduler) to decay deafness over time.
        /// </summary>
        /// <param name="deltaTime">Delta time since last tick</param>
        public void Tick(float deltaTime)
        {
            if (Time.time >= _deafUntilTime)
            {
                _deafness = Mathf.MoveTowards(_deafness, 0f, RecoveryRate * deltaTime);
            }
        }

        /// <summary>
        /// Instantly clears deafness. Use with caution.
        /// </summary>
        public void Clear()
        {
            _deafness = 0f;
            _deafUntilTime = 0f;
        }
    }
}
