#nullable enable

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Defines a bot's visual perception profile.
    /// Simulates response to light, suppression, and temporary blindness for realism.
    /// Used by <see cref="BotPerceptionSystem"/> to adjust sensory behavior dynamically.
    /// </summary>
    public sealed class BotVisionProfile
    {
        #region Defaults

        private const float _defaultAdaptationSpeed = 1.25f;
        private const float _defaultMaxBlindness = 1.0f;
        private const float _defaultLightSensitivity = 1.0f;
        private const float _defaultAggressionResponse = 0.85f;

        #endregion

        #region Properties: Flash / Flare Sensitivity

        /// <summary>
        /// Speed of recovery from flash or flare blindness.
        /// Higher values = faster visual clarity return. Default: 1.25
        /// </summary>
        public float AdaptationSpeed { get; set; } = _defaultAdaptationSpeed;

        /// <summary>
        /// Max flash blindness level. 1 = fully blind, 0 = immune.
        /// </summary>
        public float MaxBlindness { get; set; } = _defaultMaxBlindness;

        /// <summary>
        /// How sensitive the bot is to flashlight glare or flare bursts.
        /// </summary>
        public float LightSensitivity { get; set; } = _defaultLightSensitivity;

        #endregion

        #region Properties: Suppression Response

        /// <summary>
        /// Visual penalty multiplier while under suppression.
        /// Higher values cause more tunnel vision and reduced perception.
        /// </summary>
        public float AggressionResponse { get; set; } = _defaultAggressionResponse;

        #endregion

        #region Utilities

        /// <summary>
        /// Resets all profile values to their default balanced configuration.
        /// </summary>
        public void Reset()
        {
            AdaptationSpeed = _defaultAdaptationSpeed;
            MaxBlindness = _defaultMaxBlindness;
            LightSensitivity = _defaultLightSensitivity;
            AggressionResponse = _defaultAggressionResponse;
        }

        /// <summary>
        /// Creates a new default-configured vision profile.
        /// </summary>
        public static BotVisionProfile CreateDefault()
        {
            return new BotVisionProfile
            {
                AdaptationSpeed = _defaultAdaptationSpeed,
                MaxBlindness = _defaultMaxBlindness,
                LightSensitivity = _defaultLightSensitivity,
                AggressionResponse = _defaultAggressionResponse
            };
        }

        #endregion
    }
}
