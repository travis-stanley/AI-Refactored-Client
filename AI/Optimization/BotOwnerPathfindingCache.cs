#nullable enable

using AIRefactored.AI.Memory;
using AIRefactored.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Caches per-bot NavMesh paths and retreat scoring to avoid redundant computation.
    /// Also supports scoring fallback cover positions and squad-level broadcast logic.
    /// </summary>
    public class BotOwnerPathfindingCache
    {
        #region Fields

        private readonly Dictionary<string, List<Vector3>> _pathCache = new Dictionary<string, List<Vector3>>();
        private readonly Dictionary<string, float> _coverWeights = new Dictionary<string, float>();
        private readonly HashSet<string> _usedKeysThisFrame = new HashSet<string>();

        #endregion

        #region Path Caching

        /// <summary>
        /// Returns a cached or newly computed NavMesh path from bot to destination.
        /// Falls back to a straight-line path if NavMesh fails.
        /// </summary>
        public List<Vector3> GetOptimizedPath(BotOwner botOwner, Vector3 destination)
        {
            if (!IsAIBot(botOwner) || botOwner?.Profile?.Id == null)
                return new List<Vector3> { destination };

            string botId = botOwner.Profile.Id;
            string key = botId + "_" + destination.ToString("F2");

            if (_pathCache.TryGetValue(key, out var cachedPath))
                return cachedPath;

            var path = BuildNavPath(botOwner.Position, destination);
            _pathCache[key] = path;

            return path;
        }

        /// <summary>
        /// Clears all cached paths and transient keys.
        /// </summary>
        public void Clear()
        {
            _pathCache.Clear();
            _usedKeysThisFrame.Clear();
        }

        #endregion

        #region NavMesh Logic

        /// <summary>
        /// Computes a NavMesh path between two points. Falls back to a 2-point line if invalid.
        /// </summary>
        private List<Vector3> BuildNavPath(Vector3 origin, Vector3 target)
        {
            var navPath = new NavMeshPath();

            if (NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, navPath) &&
                navPath.status == NavMeshPathStatus.PathComplete)
            {
                return new List<Vector3>(navPath.corners);
            }

            // Fallback
            return new List<Vector3> { origin, target };
        }

        #endregion

        #region Cover Scoring

        /// <summary>
        /// Stores a weight (desirability) score for a cover point.
        /// </summary>
        public void RegisterCoverNode(string mapId, Vector3 pos, float score)
        {
            string key = mapId + "_" + RoundVector3ToKey(pos);
            if (!_coverWeights.ContainsKey(key))
                _coverWeights[key] = Mathf.Clamp(score, 0.1f, 10f);
        }

        /// <summary>
        /// Retrieves cover score for a location, or 1.0 (neutral) if unknown.
        /// </summary>
        public float GetCoverWeight(string mapId, Vector3 pos)
        {
            string key = mapId + "_" + RoundVector3ToKey(pos);
            return _coverWeights.TryGetValue(key, out float weight) ? weight : 1f;
        }

        #endregion

        #region Group Danger Broadcast

        /// <summary>
        /// Broadcasts a fallback position to other bots via squad memory.
        /// Useful for group panic or shared danger awareness.
        /// </summary>
        public void BroadcastRetreat(BotOwner botOwner, Vector3 point)
        {
            if (!IsAIBot(botOwner) || botOwner.BotsGroup == null || string.IsNullOrEmpty(botOwner.ProfileId))
                return;

            string map = GameWorldHandler.GetCurrentMapName();
            BotMemoryStore.AddDangerZone(map, point, DangerTriggerType.Panic, 5f);
        }

        #endregion

        #region Helpers

        private static bool IsAIBot(BotOwner bot)
        {
            var player = bot?.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        private static string RoundVector3ToKey(Vector3 v)
        {
            return $"{v.x:F1}_{v.y:F1}_{v.z:F1}";
        }

        #endregion
    }
}
