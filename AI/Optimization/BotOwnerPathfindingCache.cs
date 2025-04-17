#nullable enable

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using EFT;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;
using AIRefactored.Core;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Caches pathfinding decisions per bot to avoid redundant calculations.
    /// Used for fallback routing and tactical NavMesh pathing.
    /// </summary>
    public class BotOwnerPathfindingCache
    {
        #region Fields

        private readonly Dictionary<string, List<Vector3>> _pathCache = new();
        private readonly Dictionary<string, float> _coverWeights = new();

        private readonly List<Vector3> _navBuffer = new(16);
        private readonly HashSet<string> _usedKeysThisFrame = new();

        #endregion

        #region Public API

        /// <summary>
        /// Returns a cached or newly computed path for the given AI bot to the desired destination.
        /// </summary>
        public List<Vector3> GetOptimizedPath(BotOwner botOwner, Vector3 destination)
        {
            if (!IsAIBot(botOwner) || botOwner?.Profile?.Id == null)
                return new List<Vector3> { destination };

            string botId = botOwner.Profile.Id;
            string key = botId + "_" + destination.ToString("F2");

            if (_pathCache.ContainsKey(key))
                return _pathCache[key];

            var path = BuildNavPath(botOwner.Position, destination);
            _pathCache[key] = path;

            return path;
        }

        /// <summary>
        /// Clears all cached paths and used keys (e.g., on death or cleanup).
        /// </summary>
        public void ClearCache()
        {
            _pathCache.Clear();
            _usedKeysThisFrame.Clear();
        }

        #endregion

        #region NavMesh Logic

        /// <summary>
        /// Attempts to build a NavMesh path. Falls back to straight-line if unavailable.
        /// </summary>
        private List<Vector3> BuildNavPath(Vector3 origin, Vector3 target)
        {
            var navPath = new NavMeshPath();
            if (NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, navPath) &&
                navPath.status == NavMeshPathStatus.PathComplete)
            {
                return new List<Vector3>(navPath.corners);
            }

            return new List<Vector3> { origin, target };
        }

        #endregion

        #region Cover Weighting

        /// <summary>
        /// Assigns cover weight to a fallback node.
        /// </summary>
        public void RegisterCoverNode(string mapId, Vector3 pos, float score)
        {
            string key = mapId + "_" + pos.ToString("F1");
            if (!_coverWeights.ContainsKey(key))
            {
                _coverWeights[key] = Mathf.Clamp(score, 0.1f, 10f);
            }
        }

        /// <summary>
        /// Gets cover weight for a fallback node (1.0 = neutral).
        /// </summary>
        public float GetCoverWeight(string mapId, Vector3 pos)
        {
            string key = mapId + "_" + pos.ToString("F1");
            return _coverWeights.ContainsKey(key) ? _coverWeights[key] : 1f;
        }

        #endregion

        #region Squad Sync Logic

        /// <summary>
        /// Broadcasts a fallback decision across squad via memory.
        /// </summary>
        public void BroadcastRetreat(BotOwner botOwner, Vector3 point)
        {
            if (!IsAIBot(botOwner) || botOwner?.BotsGroup == null || botOwner.ProfileId == null)
                return;

            string map = GameWorldHandler.GetCurrentMapName();
            BotMemoryStore.AddDangerZone(map, point, DangerTriggerType.Panic, 5f);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Ensures logic only applies to real AI bots (not players or coop-controlled).
        /// </summary>
        private static bool IsAIBot(BotOwner bot)
        {
            var player = bot?.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        #endregion
    }
}
