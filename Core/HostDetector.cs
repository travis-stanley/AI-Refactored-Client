#nullable enable

using AIRefactored.Runtime;
using Fika.Core.Coop.Utils;

namespace AIRefactored.Core
{
    /// <summary>
    /// Determines whether the current client instance is acting as host (FIKA GroupLeader or SPT singleplayer).
    /// Used for logic requiring authoritative control or host-only operations.
    /// </summary>
    public static class HostDetector
    {
        #region Fields

        private static bool? _isHost;

        #endregion

        #region Public API

        /// <summary>
        /// Returns true if this instance is the host (FIKA GroupLeader or SPT offline).
        /// Caches the result after the first evaluation.
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
                    AIRefactoredController.Logger.LogInfo("[AI-Refactored] ✅ FIKA host detected (GroupLeader).");
                }
                else if (FikaBackendUtils.IsClient)
                {
                    _isHost = false;
                    AIRefactoredController.Logger.LogInfo("[AI-Refactored] 🧍 FIKA client detected (GroupPlayer).");
                }
                else
                {
                    _isHost = true;
                    AIRefactoredController.Logger.LogInfo("[AI-Refactored] 🔄 Defaulting to SPT host.");
                }
            }
            catch (System.Exception e)
            {
                _isHost = false;
                AIRefactoredController.Logger.LogError($"[AI-Refactored] ❌ HostDetector failed: {e.Message}");
            }

            return _isHost.Value;
        }

        #endregion
    }
}
