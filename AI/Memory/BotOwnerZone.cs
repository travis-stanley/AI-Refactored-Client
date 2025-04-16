#nullable enable

using System.Collections.Generic;
using EFT.Game.Spawning;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Represents a custom spawn zone tied to a bot. Used for coordination, fallback, and debug.
    /// </summary>
    public class BotOwnerZone : MonoBehaviour
    {
        /// <summary>
        /// Unique zone identifier used in syncing or internal logic.
        /// </summary>
        public string Id = "default";

        /// <summary>
        /// Human-readable label (for debug overlays or logging).
        /// </summary>
        public string NameZone = "Zone";

        /// <summary>
        /// Role assigned to this zone (e.g. PMC, SCAV, SNIPER).
        /// </summary>
        public string Role = "Generic";

        /// <summary>
        /// The spawn points explicitly assigned to this zone.
        /// </summary>
        public List<ISpawnPoint> SpawnPoints = new();

        /// <summary>
        /// Optional logic injected via Harmony (intentionally empty).
        /// </summary>
        public void Awake()
        {
            // Stub for Harmony patching or runtime injection
        }

        /// <summary>
        /// Optional: Current runtime zone assigned (by teammates, sync, etc.).
        /// </summary>
        public string? CurrentZone { get; private set; }

        /// <summary>
        /// Assign a new zone ID to this bot.
        /// </summary>
        public void AssignZone(string zoneId)
        {
            CurrentZone = zoneId;
        }
    }
}
