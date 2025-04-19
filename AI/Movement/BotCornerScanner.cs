#nullable enable

using AIRefactored.AI.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Detects corners and triggers lean-peeking or pauses for tactical movement through doorways and blind angles.
    /// </summary>
    public class BotCornerScanner
    {
        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly BotPersonalityProfile _personality;

        private const float WallCheckDistance = 1.6f;
        private const float WallCheckHeight = 1.4f;
        private const float PauseDuration = 0.4f;
        private const float WallAngleThreshold = 0.7f;

        private float _pauseUntil = 0f;

        public BotCornerScanner(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _personality = cache.AIRefactoredBotOwner?.PersonalityProfile ?? new BotPersonalityProfile();
        }

        public void Tick(float time)
        {
            if (_bot.IsDead || _bot.Mover == null || _bot.Memory?.GoalEnemy != null)
                return;

            if (time < _pauseUntil)
                return;

            if (_personality.Caution < 0.4f && !_personality.IsCamper && !_personality.IsSilentHunter)
                return;

            Vector3 origin = _bot.Position + Vector3.up * WallCheckHeight;
            Vector3 left = -_bot.Transform.right;
            Vector3 right = _bot.Transform.right;

            if (Physics.Raycast(origin, left, out RaycastHit hitLeft, WallCheckDistance))
            {
                if (IsAngledWall(hitLeft.normal, left))
                {
                    TriggerLean(BotTiltType.left, time);
                    return;
                }
            }

            if (Physics.Raycast(origin, right, out RaycastHit hitRight, WallCheckDistance))
            {
                if (IsAngledWall(hitRight.normal, right))
                {
                    TriggerLean(BotTiltType.right, time);
                    return;
                }
            }

            // No valid wall detected – cancel lean via tiltOff hack
            if (_cache.Tilt != null)
            {
                _cache.Tilt.tiltOff = Time.time - 1f;  // force expiration
                _cache.Tilt.ManualUpdate();           // immediately reset lean
            }
        }

        private void TriggerLean(BotTiltType side, float time)
        {
            _cache.Tilt?.Set(side);
            _pauseUntil = time + PauseDuration;
        }

        private bool IsAngledWall(Vector3 wallNormal, Vector3 scanDir)
        {
            float dot = Vector3.Dot(wallNormal, scanDir.normalized);
            return dot < WallAngleThreshold;
        }
    }
}
