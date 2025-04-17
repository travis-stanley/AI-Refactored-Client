#nullable enable

using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Interface for bots that respond to directional flash exposure.
    /// Implementers simulate vision impairment or panic when exposed to intense light sources.
    /// </summary>
    public interface IFlashReactiveBot
    {
        #region Exposure Response

        /// <summary>
        /// Called when a bot is hit by a directional flash or spotlight.
        /// </summary>
        /// <param name="lightOrigin">The world position of the light source.</param>
        void OnFlashExposure(Vector3 lightOrigin);

        #endregion
    }
}
