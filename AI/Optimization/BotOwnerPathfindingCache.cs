#nullable enable

using System.Collections.Generic;
using UnityEngine;
using EFT;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Caches pathfinding decisions per bot to avoid redundant calculations.
    /// Used for simple fallback routes and future NavMesh integrations.
    /// </summary>
    public class BotOwnerPathfindingCache
    {
        private readonly Dictionary<string, List<Vector3>> _cache = new();

        /// <summary>
        /// Returns a cached or newly computed path for this bot.
        /// </summary>
        public List<Vector3> GetOptimizedPath(BotOwner botOwner, Vector3 destination)
        {
            if (botOwner?.Profile?.Id == null)
                return new List<Vector3> { destination };

            string botId = botOwner.Profile.Id;

            if (_cache.TryGetValue(botId, out var cachedPath))
                return cachedPath;

            var newPath = CalculatePath(botOwner, destination);
            _cache[botId] = newPath;
            return newPath;
        }

        /// <summary>
        /// Very basic direct-path fallback. Expand with tactical nav or NavMesh logic as needed.
        /// </summary>
        private List<Vector3> CalculatePath(BotOwner botOwner, Vector3 destination)
        {
            Vector3 startPos = botOwner.Transform?.position ?? destination;

            return new List<Vector3>
            {
                startPos,
                destination
            };
        }

        /// <summary>
        /// Clears all cached paths (e.g., after respawn or reset).
        /// </summary>
        public void ClearCache()
        {
            _cache.Clear();
        }
    }
}
