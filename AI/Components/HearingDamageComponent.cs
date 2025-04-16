using UnityEngine;

namespace AIRefactored.AI.Components
{
    /// <summary>
    /// Simulates hearing damage and deafening effects from loud sounds.
    /// Deafness level affects how well bots perceive sound and react to stimuli.
    /// </summary>
    public class HearingDamageComponent : MonoBehaviour
    {
        /// <summary>Current deafness level (0 = normal, 1 = fully deafened).</summary>
        public float Deafness { get; private set; } = 0f;

        /// <summary>Time left for recovery in seconds.</summary>
        private float _recoveryTimeLeft = 0f;

        /// <summary>Rate of recovery per second.</summary>
        private const float RecoveryRate = 0.25f;

        /// <summary>Is the bot currently considered deafened?</summary>
        public bool IsDeafened => Deafness > 0.2f;

        private void Update()
        {
            if (_recoveryTimeLeft > 0f)
            {
                _recoveryTimeLeft -= Time.deltaTime;
                Deafness = Mathf.MoveTowards(Deafness, 0f, RecoveryRate * Time.deltaTime);
            }
        }

        /// <summary>Apply a new deafening effect.</summary>
        public void ApplyDeafness(float intensity, float duration)
        {
            Deafness = Mathf.Clamp01(Mathf.Max(Deafness, intensity));
            _recoveryTimeLeft = Mathf.Max(_recoveryTimeLeft, duration);
        }
    }
}
