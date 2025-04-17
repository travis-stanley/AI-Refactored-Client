#nullable enable

using UnityEngine;
using EFT;
using Comfort.Common;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Handles panic behavior for bots. Triggers retreat/fallback when blinded or under extreme threat.
    /// </summary>
    public class BotPanicHandler : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _panicStartTime = -1f;
        private bool _isPanicking = false;

        private const float PanicDuration = 3.5f;
        private const float PanicCooldown = 5.0f;
        private float _lastPanicExitTime = -99f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
        }

        private void Update()
        {
            if (_bot == null || _cache == null || IsHumanPlayer())
                return;

            float now = Time.time;

            if (!_isPanicking)
            {
                if (now > _lastPanicExitTime + PanicCooldown && ShouldTriggerPanic())
                {
                    StartPanic(now);
                }
            }
            else if (now - _panicStartTime > PanicDuration)
            {
                EndPanic(now);
            }
        }

        #endregion

        #region Panic Triggers

        /// <summary>
        /// Externally forces panic if conditions are safe to do so.
        /// </summary>
        public void TriggerPanic()
        {
            if (_bot == null || IsHumanPlayer())
                return;

            float now = Time.time;
            if (!_isPanicking && now > _lastPanicExitTime + PanicCooldown)
                StartPanic(now);
        }

        /// <summary>
        /// Checks if current bot state warrants panic.
        /// </summary>
        private bool ShouldTriggerPanic()
        {
            if (_cache?.FlashGrenade?.IsFlashed() == true)
                return true;

            var hp = _bot?.HealthController?.GetBodyPartHealth(EBodyPart.Common);
            return hp.HasValue && hp.Value.Current < 25f;
        }

        #endregion

        #region Panic Behavior

        /// <summary>
        /// Begins panic behavior and paths to fallback cover.
        /// </summary>
        private void StartPanic(float now)
        {
            if (_bot == null || _cache == null)
                return;

            _isPanicking = true;
            _panicStartTime = now;

            Vector3 fallbackDir = -_bot.LookDirection.normalized;
            Vector3 fallbackPos = _bot.Position + fallbackDir * 8f;

            if (Physics.Raycast(_bot.Position, fallbackDir, out RaycastHit hit, 8f))
            {
                fallbackPos = hit.point - fallbackDir;
            }

            if (_cache.PathCache != null)
            {
                var path = BotCoverRetreatPlanner.GetCoverRetreatPath(_bot, fallbackDir, _cache.PathCache);
                if (path.Count > 0)
                    fallbackPos = path[path.Count - 1];
            }

            BotMovementHelper.SmoothMoveTo(_bot, fallbackPos, allowSlowEnd: false, cohesionScale: 1f);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);

            var world = Singleton<GameWorld>.Instance;
            if (world?.MainPlayer?.Location != null)
            {
                string mapId = world.MainPlayer.Location;
                BotMemoryStore.AddDangerZone(mapId, _bot.Position, DangerTriggerType.Panic, 0.6f);
            }
        }

        /// <summary>
        /// Ends panic and restores normal memory state.
        /// </summary>
        private void EndPanic(float now)
        {
            _isPanicking = false;
            _lastPanicExitTime = now;
            _bot?.Memory?.SetLastTimeSeeEnemy();
            _bot?.Memory?.CheckIsPeace();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// True if the associated player is a human, not an AI.
        /// </summary>
        private bool IsHumanPlayer()
        {
            return _bot?.GetPlayer != null && !_bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
