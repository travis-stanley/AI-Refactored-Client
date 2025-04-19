#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Components
{
    /// <summary>
    /// Tracks and applies hearing loss effects to a bot after loud sounds (gunfire, explosions, flashbangs).
    /// Used to dampen sound detection and simulate deafness.
    /// </summary>
    public class HearingDamageComponent
    {
        private float _deafness = 0f;
        private float _recoveryRate = 0.3f; // Per second
        private float _deafUntilTime = 0f;

        /// <summary>
        /// Current deafness level (0.0 = full hearing, 1.0 = fully deaf).
        /// </summary>
        public float Deafness => _deafness;

        /// <summary>
        /// True if deafness is significant enough to impair sound detection.
        /// </summary>
        public bool IsDeafened => _deafness > 0.2f;

        /// <summary>
        /// Amount by which sound volume should be reduced (1.0 = no sound, 0.0 = unaffected).
        /// Use to attenuate audio-based detection or awareness.
        /// </summary>
        public float VolumeModifier => Mathf.Clamp01(1f - Deafness);

        /// <summary>
        /// Time remaining before deafness completely wears off.
        /// </summary>
        public float RemainingTime => Mathf.Max(0f, _deafUntilTime - Time.time);

        /// <summary>
        /// Applies a deafening effect to the bot. The duration is cumulative if overlapping.
        /// </summary>
        public void ApplyDeafness(float intensity, float duration)
        {
            intensity = Mathf.Clamp01(intensity);
            _deafness = Mathf.Max(_deafness, intensity);
            _deafUntilTime = Mathf.Max(_deafUntilTime, Time.time + duration);
        }

        /// <summary>
        /// Called each frame to recover from deafness over time.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (Time.time >= _deafUntilTime)
            {
                _deafness = Mathf.MoveTowards(_deafness, 0f, _recoveryRate * deltaTime);
            }
        }

        /// <summary>
        /// Immediately clears all hearing effects.
        /// </summary>
        public void Clear()
        {
            _deafness = 0f;
            _deafUntilTime = 0f;
        }
    }
}
