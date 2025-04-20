#nullable enable

using AIRefactored.AI.Core;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Selects and maintains the most viable target based on visibility, range, and threat level.
    /// Allows AI to react dynamically to threats without constant target snapping.
    /// </summary>
    public class BotThreatSelector
    {
        private readonly BotComponentCache _cache;
        private readonly BotOwner _bot;

        private IPlayer? _currentTarget;
        private float _nextEvaluateTime = 0f;
        private float _lastTargetSwitchTime = -999f;

        private const float EvaluationCooldown = 0.35f;
        private const float SwitchCooldown = 2.0f;
        private const float MaxScanDistance = 120f;

        public IPlayer? CurrentTarget => _currentTarget;

        public BotThreatSelector(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot!;
        }

        public void Tick(float time)
        {
            if (_bot == null || _bot.IsDead || !_bot.IsAI || _bot.GetPlayer == null)
                return;

            if (time < _nextEvaluateTime)
                return;

            _nextEvaluateTime = time + EvaluationCooldown;

            IPlayer? bestTarget = null;
            float bestScore = float.MinValue;

            var candidates = Singleton<GameWorld>.Instance?.RegisteredPlayers;
            if (candidates == null)
                return;

            for (int i = 0; i < candidates.Count; i++)
            {
                var other = candidates[i];
                if (other == null || other.ProfileId == _bot.ProfileId || !other.HealthController.IsAlive)
                    continue;

                if (!_bot.BotsGroup.IsEnemy(other))
                    continue;

                float score = ScoreTarget(other, time);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = other;
                }
            }

            if (bestTarget == null)
                return;

            if (_currentTarget == null)
            {
                _currentTarget = bestTarget;
                _lastTargetSwitchTime = time;
                return;
            }

            // Avoid erratic switching unless new target is clearly superior
            float currentScore = ScoreTarget(_currentTarget, time);
            if (bestScore > currentScore + 10f && time > _lastTargetSwitchTime + SwitchCooldown)
            {
                _currentTarget = bestTarget;
                _lastTargetSwitchTime = time;
            }
        }

        private float ScoreTarget(IPlayer candidate, float time)
        {
            float distance = Vector3.Distance(_bot.Position, candidate.Position);
            if (distance > MaxScanDistance)
                return float.MinValue;

            float distScore = Mathf.Clamp(MaxScanDistance - distance, 0f, MaxScanDistance);
            float visibilityBonus = 0f;

            if (_bot.EnemiesController.EnemyInfos.TryGetValue(candidate, out var info))
            {
                if (info.IsVisible)
                {
                    visibilityBonus += 25f;

                    // Prefer very recently spotted enemies
                    if (info.PersonalLastSeenTime + 2.0f > time)
                        visibilityBonus += 10f;
                }
            }

            return distScore + visibilityBonus;
        }

        public void ResetTarget()
        {
            _currentTarget = null;
        }
    }
}
