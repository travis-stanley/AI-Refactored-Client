#nullable enable

using System.Collections.Generic;
using AIRefactored.AI;
using AIRefactored.AI.Hotspots;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Zones
{
    /// <summary>
    /// Tracks zone-related state for a single bot — including patrol/fallback zones.
    /// </summary>
    public class BotOwnerZone : MonoBehaviour
    {
        private BotOwner? _bot;

        /// <summary>
        /// The bot's current assigned patrol point (not fallback).
        /// </summary>
        public Vector3? CurrentZone { get; private set; }

        /// <summary>
        /// Temporary fallback zone due to panic, suppression, or damage.
        /// </summary>
        public Vector3? FallbackZone { get; private set; }

        /// <summary>
        /// Optional string ID (used by sync or debug).
        /// </summary>
        public string? ZoneId { get; private set; }

        private float updateTimer = 0f;
        private float updateInterval = 1.5f;

        private void Start()
        {
            _bot = GetComponent<BotOwner>();
            AssignInitialZone();
        }

        private void Update()
        {
            updateTimer += Time.deltaTime;
            if (updateTimer < updateInterval || _bot == null || _bot.IsDead)
                return;

            updateTimer = 0f;

            if (FallbackZone.HasValue)
            {
                float dist = Vector3.Distance(_bot.Position, FallbackZone.Value);
                if (dist < 3f)
                {
                    ResumePatrol();
                }
                else
                {
                    _bot.GoToPoint(FallbackZone.Value, slowAtTheEnd: false);
                }
            }
        }

        private void AssignInitialZone()
        {
            if (_bot == null || _bot.Profile == null)
                return;

            string id = _bot.Profile.Id;
            var profile = BotRegistry.Get(id);
            var hotspots = HotspotLoader.GetHotspotsForCurrentMap(_bot.Profile.Info.Settings.Role);

            if (hotspots == null || hotspots.Points.Count == 0)
                return;

            int index = UnityEngine.Random.Range(0, hotspots.Points.Count);
            Vector3 selected = hotspots.Points[index];

            CurrentZone = selected;
            ZoneId = $"zone_{index}_{_bot.Profile.Info.Nickname}";

            _bot.GoToPoint(selected, slowAtTheEnd: true);
        }

        /// <summary>
        /// Assign a fallback point due to panic or suppression.
        /// </summary>
        public void TriggerFallback(Vector3 point)
        {
            FallbackZone = point;
        }

        /// <summary>
        /// Resumes normal patrol behavior after fallback.
        /// </summary>
        public void ResumePatrol()
        {
            FallbackZone = null;

            if (CurrentZone.HasValue && _bot != null)
            {
                _bot.GoToPoint(CurrentZone.Value, slowAtTheEnd: true);
            }
        }

        /// <summary>
        /// Returns true if a fallback zone is currently active.
        /// </summary>
        public bool IsFallbackActive()
        {
            return FallbackZone.HasValue;
        }

        /// <summary>
        /// Assign a zone ID dynamically (e.g., from sync logic).
        /// </summary>
        public void AssignZone(string zoneId)
        {
            ZoneId = zoneId;
        }
    }
}
