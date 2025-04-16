namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Defines light sensitivity, adaptation, and visual aggression response for bot types.
    /// Used by BotPerceptionSystem.
    /// </summary>
    public class BotVisionProfile
    {
        public float AdaptationSpeed = 1.5f;
        public float MaxBlindness = 1.0f;
        public float LightSensitivity = 1.0f;
        public float AggressionResponse = 1.0f;
    }
}
