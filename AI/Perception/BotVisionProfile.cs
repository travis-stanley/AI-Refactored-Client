namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Defines light sensitivity, adaptation rate, and visual suppression response for bot types.
    /// Used by the BotPerceptionSystem to simulate human-like vision modulation.
    /// </summary>
    public class BotVisionProfile
    {
        #region Vision Modulation

        /// <summary>
        /// Multiplier applied to vision range recovery (e.g. how fast bot regains sight after flash).
        /// </summary>
        public float AdaptationSpeed = 1.5f;

        /// <summary>
        /// Maximum blindness penalty a bot can experience from flash exposure.
        /// </summary>
        public float MaxBlindness = 1.0f;

        /// <summary>
        /// How sensitive the bot is to bright light sources (e.g. flares, flashlights).
        /// </summary>
        public float LightSensitivity = 1.0f;

        /// <summary>
        /// Suppression-based vision penalty multiplier (e.g. how vision is reduced under stress).
        /// </summary>
        public float AggressionResponse = 1.0f;

        #endregion
    }
}
