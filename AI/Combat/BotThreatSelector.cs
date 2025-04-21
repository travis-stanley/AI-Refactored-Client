#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Selects and maintains the most viable threat target based on visibility, proximity, and personality bias.
    /// Prevents unnecessary target snapping and handles scoring logic per-frame.
    /// </summary>
    public class BotThreatSelector
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private readonly BotOwner _bot;
        private readonly BotPersonalityProfile _profile;

        private IPlayer? _currentTarget;
        private float _nextEvaluateTime = 0f;
        private float _lastTargetSwitchTime = -999f;

        private const float EvaluationCooldown = 0.35f;
        private const float SwitchCooldown = 2.0f;
        private const float MaxScanDistance = 120f;

        #endregion

        #region Properties

        /// <summary>
        /// Currently selected threat target, if any.
        /// </summary>
        public IPlayer? CurrentTarget => _currentTarget;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a new threat selector for the specified bot.
        /// </summary>
        /// <param name="cache">Component cache for the bot.</param>
        public BotThreatSelector(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot!;
            _profile = cache.AIRefactoredBotOwner?.PersonalityProfile ?? new BotPersonalityProfile();
        }

        #endregion

        #region Main Logic

        /// <summary>
        /// Called each tick to re-evaluate the most viable threat.
        /// </summary>
        /// <param name="time">Current world time.</param>
        public void Tick(float time)
        {
            if (_bot == null || _bot.IsDead || !_bot.IsAI || _bot.GetPlayer == null)
                return;

            if (time < _nextEvaluateTime)
                return;

            _nextEvaluateTime = time + EvaluationCooldown;

            var candidates = GameWorldHandler.GetAllAlivePlayers();

            IPlayer? bestTarget = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < candidates.Count; i++)
            {
                var other = candidates[i];
                if (other == null || other.ProfileId == _bot.ProfileId || !other.HealthController.IsAlive)
                    continue;

                if (_bot.BotsGroup == null || !_bot.BotsGroup.IsEnemy(other))
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

            float currentScore = ScoreTarget(_currentTarget, time);
            if (bestScore > currentScore + 10f && time > _lastTargetSwitchTime + SwitchCooldown)
            {
                _currentTarget = bestTarget;
                _lastTargetSwitchTime = time;
            }
        }

        #endregion

        #region Scoring

        /// <summary>
        /// Calculates a threat score based on distance, visibility, and personality modifiers.
        /// </summary>
        /// <param name="candidate">Candidate enemy to score.</param>
        /// <param name="time">Current world time.</param>
        /// <returns>Score for prioritization.</returns>
        private float ScoreTarget(IPlayer candidate, float time)
        {
            float distance = Vector3.Distance(_bot.Position, candidate.Position);
            if (distance > MaxScanDistance)
                return float.MinValue;

            float distScore = Mathf.Clamp(MaxScanDistance - distance, 0f, MaxScanDistance);
            float visibilityBonus = 0f;

            if (_bot.EnemiesController?.EnemyInfos != null &&
                _bot.EnemiesController.EnemyInfos.TryGetValue(candidate, out var info))
            {
                if (info.IsVisible)
                {
                    visibilityBonus += 25f;

                    if (info.PersonalLastSeenTime + 2.0f > time)
                        visibilityBonus += 10f;

                    if (_profile.Caution > 0.6f)
                        visibilityBonus += 5f;
                }
            }

            return distScore + visibilityBonus;
        }

        #endregion

        #region Reset

        /// <summary>
        /// Clears the current target, forcing re-selection.
        /// </summary>
        public void ResetTarget()
        {
            _currentTarget = null;
        }

        #endregion
    }
}
