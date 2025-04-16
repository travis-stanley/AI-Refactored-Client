using Fika.Core.Coop.Utils;
using UnityEngine;

namespace AIRefactored.Core
{
    /// <summary>
    /// Determines whether this client instance is the host, using FIKA or SPT context.
    /// </summary>
    public static class HostDetector
    {
        private static bool? _isHost;

        public static bool IsHost()
        {
            if (_isHost.HasValue)
                return _isHost.Value;

            try
            {
                // If MatchingType is GroupLeader → we are the host in Coop
                if (FikaBackendUtils.IsServer)
                {
                    Debug.Log("[AI-Refactored] Detected FIKA GroupLeader (host).");
                    _isHost = true;
                    return true;
                }

                // If MatchingType is GroupPlayer → client in Coop
                if (FikaBackendUtils.IsClient)
                {
                    Debug.Log("[AI-Refactored] Detected FIKA GroupPlayer (client).");
                    _isHost = false;
                    return false;
                }

                // Fallback → assume offline SPT or unknown type
                Debug.Log("[AI-Refactored] Detected SPT (Single) or undefined. Defaulting to host.");
                _isHost = true;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[AI-Refactored] HostDetector failed: {e.Message}");
                _isHost = false;
                return false;
            }
        }
    }
}
