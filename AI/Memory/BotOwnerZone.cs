#nullable enable

using System.Collections.Generic;
using EFT;
using EFT.Game.Spawning;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Represents a custom spawn zone tied to a bot. 
    /// Used for coordination, fallback routing, and debug tracking.
    /// </summary>
    public class BotOwnerZone : MonoBehaviour
    {
        #region Fields

        public string Id = "default";
        public string NameZone = "Zone";
        public string Role = "Generic";
        public List<ISpawnPoint> SpawnPoints { get; private set; } = new();
        public string? CurrentZone { get; private set; }
        public string? ZoneId { get; private set; }

        private Vector3? _fallbackZone;
        private const float ReentryThreshold = 3f;

        private float _checkCooldown = 1.25f;
        private float _lastCheckTime = -999f;

        private BotOwner? _bot;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
        }

        private void Update()
        {
            if (_bot == null || _bot.IsDead || !_fallbackZone.HasValue)
                return;

            if (_bot.GetPlayer?.IsYourPlayer == true)
                return; // 🛑 Skip fallback logic for human players (FIKA, Coop)

            if (Time.time < _lastCheckTime + _checkCooldown)
                return;

            _lastCheckTime = Time.time;

            if (Vector3.Distance(_bot.Position, _fallbackZone.Value) <= ReentryThreshold)
            {
                _fallbackZone = null;
                if (CurrentZone != null)
                {
                    _bot.GoToPoint(_bot.Position, slowAtTheEnd: true); // resume zone behavior
                }
            }
            else
            {
                _bot.GoToPoint(_fallbackZone.Value, slowAtTheEnd: false);
            }
        }

        #endregion

        #region Zone API

        public void AssignZone(string zoneId)
        {
            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                ZoneId = zoneId.Trim();
                CurrentZone = zoneId.Trim();
            }
        }

        public bool HasZone()
        {
            return !string.IsNullOrEmpty(CurrentZone);
        }

        public bool TryGetAssignedZone(out string zone)
        {
            zone = CurrentZone ?? string.Empty;
            return HasZone();
        }

        /// <summary>
        /// Assigns a fallback point — the bot will move here temporarily until safe to resume.
        /// </summary>
        public void TriggerFallback(Vector3 position)
        {
            if (position == Vector3.zero || _bot == null)
                return;

            if (_bot.GetPlayer?.IsYourPlayer == true)
                return; // 🛑 Skip fallback triggering for human players

            _fallbackZone = position;
            _bot.GoToPoint(position, slowAtTheEnd: false);
        }

        /// <summary>
        /// Returns true if bot is actively in fallback mode.
        /// </summary>
        public bool IsFallbackActive()
        {
            return _fallbackZone.HasValue;
        }

        #endregion

        #region Debug

        public override string ToString()
        {
            return $"[Zone:{Id}] {NameZone} ({Role}) → Assigned: {CurrentZone ?? "None"} | Points: {SpawnPoints.Count}";
        }

        #endregion
    }
}
