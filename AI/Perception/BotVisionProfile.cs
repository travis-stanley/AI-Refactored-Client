#nullable enable

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Defines light sensitivity, adaptation rate, and suppression vision penalties for bots.
    /// Used by <see cref="BotPerceptionSystem"/> to simulate human-like vision response.
    /// </summary>
    public sealed class BotVisionProfile
    {
        #region Vision Modulation

        /// <summary>
        /// Multiplier applied to visual range recovery. Higher = faster recovery from flash effects.
        /// </summary>
        public float AdaptationSpeed { get; set; } = 1.5f;

        /// <summary>
        /// Maximum visual impairment caused by flash blindness (0 = no effect, 1 = fully blind).
        /// </summary>
        public float MaxBlindness { get; set; } = 1.0f;

        /// <summary>
        /// Sensitivity to intense lighting such as flares or flashlights. Affects blindness scaling.
        /// </summary>
        public float LightSensitivity { get; set; } = 1.0f;

        /// <summary>
        /// Vision penalty factor during suppression. Higher = greater reduction under fire.
        /// </summary>
        public float AggressionResponse { get; set; } = 1.0f;

        #endregion
    }
}
