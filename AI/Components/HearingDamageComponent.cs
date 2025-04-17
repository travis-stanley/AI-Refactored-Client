using UnityEngine;

namespace AIRefactored.AI.Components
{
    /// <summary>
    /// Simulates hearing damage and deafening effects from loud sounds like explosions or gunfire.
    /// Deafness affects a bot's ability to perceive auditory cues in the world.
    /// </summary>
    public class HearingDamageComponent : MonoBehaviour
    {
        #region Fields

        /// <summary>
        /// Current deafness level (0 = normal, 1 = fully deafened).
        /// </summary>
        public float Deafness { get; private set; } = 0f;

        /// <summary>
        /// Remaining recovery time in seconds.
        /// </summary>
        private float _recoveryTimeLeft = 0f;

        /// <summary>
        /// Rate at which deafness recovers per second.
        /// </summary>
        private const float RecoveryRate = 0.25f;

        #endregion

        #region Properties

        /// <summary>
        /// Whether the bot is currently considered deafened (Deafness > 0.2).
        /// </summary>
        public bool IsDeafened => Deafness > 0.2f;

        #endregion

        #region Unity Events

        private void Update()
        {
            if (_recoveryTimeLeft > 0f)
            {
                _recoveryTimeLeft -= Time.deltaTime;
                Deafness = Mathf.MoveTowards(Deafness, 0f, RecoveryRate * Time.deltaTime);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// Applies a deafening effect with a given intensity and recovery duration.
        /// If multiple deafness sources apply, the stronger one wins.
        /// </summary>
        /// <param name="intensity">Value between 0 (none) and 1 (fully deaf).</param>
        /// <param name="duration">Duration in seconds until recovery begins.</param>
        public void ApplyDeafness(float intensity, float duration)
        {
            Deafness = Mathf.Clamp01(Mathf.Max(Deafness, intensity));
            _recoveryTimeLeft = Mathf.Max(_recoveryTimeLeft, duration);
        }

        #endregion
    }
}
