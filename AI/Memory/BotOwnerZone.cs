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
        /// Human-readable label (for debug overlays or logs).
        /// </summary>
        public string NameZone = "Zone";

        /// <summary>
        /// Role assigned to this zone (e.g. PMC, SCAV, SNIPER).
        /// </summary>
        public string Role = "Generic";

        /// <summary>
        /// The spawn points explicitly assigned to this zone.
        /// </summary>
        public List<ISpawnPoint> SpawnPoints { get; private set; } = new();

        /// <summary>
        /// Optional runtime zone assigned (e.g. from sync system).
        /// </summary>
        public string? CurrentZone { get; private set; }

        /// <summary>
        /// Assign a new zone ID to this bot.
        /// </summary>
        public void AssignZone(string zoneId)
        {
            if (!string.IsNullOrEmpty(zoneId))
            {
                CurrentZone = zoneId;
            }
        }

        /// <summary>
        /// Whether this bot currently has a dynamic zone assigned.
        /// </summary>
        public bool HasZone() => !string.IsNullOrEmpty(CurrentZone);

        /// <summary>
        /// Optional logic injected via Harmony (intentionally empty).
        /// </summary>
        public void Awake()
        {
            // Stub for Harmony patching or runtime injection
        }

        /// <summary>
        /// Debug summary string.
        /// </summary>
        public override string ToString()
        {
            return $"[Zone:{Id}] {NameZone} ({Role}) → Assigned: {CurrentZone ?? "None"} | Points: {SpawnPoints.Count}";
        }
    }
}
