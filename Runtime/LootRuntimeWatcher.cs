#nullable enable

using AIRefactored.Core;
using UnityEngine;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Monitors dynamically spawned loot (e.g., from dead players or quest events).
    /// Automatically triggers a registry update after scene settle delay.
    /// </summary>
    public sealed class LootRuntimeWatcher : MonoBehaviour
    {
        #region Configuration

        private const float RefreshDelay = 0.1f;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            if (!Application.isPlaying || FikaHeadlessDetector.IsHeadless)
                return;

            Invoke(nameof(NotifyLootChanged), RefreshDelay);
        }

        #endregion

        #region Refresh Trigger

        /// <summary>
        /// Notifies the world handler to rescan loot objects.
        /// </summary>
        private void NotifyLootChanged()
        {
            if (!Application.isPlaying || FikaHeadlessDetector.IsHeadless)
                return;

            if (GameWorldHandler.IsInitialized)
                GameWorldHandler.RefreshLootRegistry();
        }

        #endregion
    }
}
