#nullable enable

using AIRefactored.AI.Looting;
using AIRefactored.Core;
using EFT.Interactive;
using UnityEngine;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Watches for loot containers associated with dead players and registers them.
    /// Prevents redundant looting and ensures corpse-container association is maintained.
    /// </summary>
    public sealed class DeadBodyObserver : MonoBehaviour
    {
        #region Configuration

        private const float ScanInterval = 1.0f;

        #endregion

        #region State

        private float _nextScanTime = 0f;

        #endregion

        #region Unity Loop

        private void Update()
        {
            if (!GameWorldHandler.IsInitialized || FikaHeadlessDetector.IsHeadless)
                return;

            float now = Time.time;
            if (now < _nextScanTime)
                return;

            _nextScanTime = now + ScanInterval;

            var containers = FindObjectsOfType<LootableContainer>();
            if (containers == null || containers.Length == 0)
                return;

            var players = GameWorldHandler.GetAllAlivePlayers();
            if (players == null || players.Count == 0)
                return;

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.HealthController == null || player.HealthController.IsAlive)
                    continue;

                string profileId = player.ProfileId;
                if (string.IsNullOrEmpty(profileId) || DeadBodyContainerCache.Contains(profileId))
                    continue;

                var root = player.Transform?.Original?.root;
                if (root == null)
                    continue;

                for (int j = 0; j < containers.Length; j++)
                {
                    var container = containers[j];
                    if (container == null || !container.enabled)
                        continue;

                    if (container.transform.root == root)
                    {
                        DeadBodyContainerCache.Register(player, container);
                        break;
                    }
                }
            }
        }

        #endregion
    }
}
