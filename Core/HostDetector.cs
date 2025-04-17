#nullable enable

using Fika.Core.Coop.Utils;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Determines whether this client instance is the host in a FIKA or SPT environment.
    /// </summary>
    public static class HostDetector
    {
        #region Fields

        private static bool? _isHost;

        #endregion

        #region Public API

        /// <summary>
        /// Returns true if this client instance is considered the host (GroupLeader or SPT singleplayer).
        /// </summary>
        public static bool IsHost()
        {
            if (_isHost.HasValue)
                return _isHost.Value;

            try
            {
                if (FikaBackendUtils.IsServer)
                {
                    _isHost = true;
                    Debug.Log("[AI-Refactored] ✅ FIKA host detected (GroupLeader).");
                }
                else if (FikaBackendUtils.IsClient)
                {
                    _isHost = false;
                    Debug.Log("[AI-Refactored] 🧍 FIKA client detected (GroupPlayer).");
                }
                else
                {
                    _isHost = true;
                    Debug.Log("[AI-Refactored] 🔄 Defaulting to SPT host.");
                }
            }
            catch (System.Exception e)
            {
                _isHost = false;
                Debug.LogError($"[AI-Refactored] ❌ HostDetector failed: {e.Message}");
            }

            return _isHost.Value;
        }

        #endregion
    }
}
