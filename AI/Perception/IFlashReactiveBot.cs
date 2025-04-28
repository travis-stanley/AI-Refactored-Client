#nullable enable

namespace AIRefactored.AI.Perception
{
    using UnityEngine;

    /// <summary>
    ///     Defines reactive behavior for bots exposed to high-intensity light sources like flashlights or flashbangs.
    ///     Implementing classes simulate temporary blindness, panic triggers, and directional threat response.
    /// </summary>
    public interface IFlashReactiveBot
    {
        /// <summary>
        ///     Triggers a flash reaction when the bot is exposed to a bright light source.
        ///     May result in suppression, blindness, panic, or reorientation.
        /// </summary>
        /// <param name="lightOrigin">World-space origin point of the light exposure.</param>
        void OnFlashExposure(Vector3 lightOrigin);
    }
}