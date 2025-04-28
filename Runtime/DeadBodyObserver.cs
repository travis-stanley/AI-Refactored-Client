#nullable enable

namespace AIRefactored.Runtime
{
    using AIRefactored.AI.Looting;
    using AIRefactored.Core;

    using EFT.Interactive;

    using UnityEngine;

    /// <summary>
    ///     Watches for loot containers associated with dead players and registers them.
    ///     Prevents redundant looting and ensures corpse-container association is maintained.
    /// </summary>
    public sealed class DeadBodyObserver : MonoBehaviour
    {
        #region Configuration

        private const float ScanInterval = 1.0f;

        #endregion

        #region State

        private float _nextScanTime;

        #endregion

        #region Unity Loop

        private void Update()
        {
            if (!GameWorldHandler.IsInitialized || FikaHeadlessDetector.IsHeadless)
                return;

            var now = Time.time;
            if (now < this._nextScanTime)
                return;

            this._nextScanTime = now + ScanInterval;

            var containers = FindObjectsOfType<LootableContainer>();
            if (containers == null || containers.Length == 0)
                return;

            var players = GameWorldHandler.GetAllAlivePlayers();
            if (players == null || players.Count == 0)
                return;

            for (var i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null || player.HealthController == null || player.HealthController.IsAlive)
                    continue;

                var profileId = player.ProfileId;
                if (string.IsNullOrEmpty(profileId) || DeadBodyContainerCache.Contains(profileId))
                    continue;

                var root = player.Transform?.Original?.root;
                if (root == null)
                    continue;

                for (var j = 0; j < containers.Length; j++)
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