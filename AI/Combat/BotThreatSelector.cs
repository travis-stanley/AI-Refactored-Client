#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.Core;
using EFT;
using System;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Selects and maintains the most viable threat target based on distance, visibility, and bot personality.
    /// Avoids excessive switching and prefers visible, nearby enemies under pressure.
    /// </summary>
    public class BotThreatSelector
    {
        #region Fields

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
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
        /// The currently selected threat target.
        /// </summary>
        public IPlayer? CurrentTarget => _currentTarget;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new threat selector for the specified bot.
        /// </summary>
        public BotThreatSelector(BotComponentCache cache)
        {
            if (cache == null || cache.Bot == null || cache.AIRefactoredBotOwner == null)
                throw new ArgumentNullException(nameof(cache));

            _cache = cache;
            _bot = cache.Bot!;
            _profile = cache.AIRefactoredBotOwner.PersonalityProfile;
        }

        #endregion

        #region Main Logic

        /// <summary>
        /// Called each tick to evaluate and potentially update the current enemy target.
        /// </summary>
        public void Tick(float time)
        {
            if (_bot == null || _bot.IsDead || !_bot.IsAI || _bot.GetPlayer == null)
                return;

            if (time < _nextEvaluateTime)
                return;

            _nextEvaluateTime = time + EvaluationCooldown;

            var players = GameWorldHandler.GetAllAlivePlayers();
            if (players == null || players.Count == 0)
                return;

            IPlayer? bestTarget = null;
            float bestScore = float.MinValue;

            for (int i = 0; i < players.Count; i++)
            {
                var candidate = players[i];
                if (candidate == null || candidate.ProfileId == _bot.ProfileId || candidate.HealthController?.IsAlive != true)
                    continue;

                if (_bot.BotsGroup == null || !_bot.BotsGroup.IsEnemy(candidate))
                    continue;

                float score = ScoreTarget(candidate, time);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = candidate;
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
            float switchThreshold = 10f;
            float cooldown = SwitchCooldown * (1f - _profile.AggressionLevel * 0.5f);

            if (bestScore > currentScore + switchThreshold && time > _lastTargetSwitchTime + cooldown)
            {
                _currentTarget = bestTarget;
                _lastTargetSwitchTime = time;
            }
        }

        #endregion

        #region Scoring

        /// <summary>
        /// Returns a dynamic threat score for the given target.
        /// </summary>
        private float ScoreTarget(IPlayer candidate, float time)
        {
            float distance = Vector3.Distance(_bot.Position, candidate.Position);
            if (distance > MaxScanDistance)
                return float.MinValue;

            float score = MaxScanDistance - distance;

            var infos = _bot.EnemiesController?.EnemyInfos;
            if (infos != null && infos.ContainsKey(candidate))
            {
                var info = infos[candidate];
                if (info != null && info.IsVisible)
                {
                    score += 25f;

                    if (info.PersonalLastSeenTime + 2f > time)
                        score += 10f;

                    if (_profile.Caution > 0.6f)
                        score += 5f;
                }
            }

            return score;
        }

        #endregion

        #region Reset

        /// <summary>
        /// Clears the current target.
        /// </summary>
        public void ResetTarget()
        {
            _currentTarget = null;
        }

        #endregion
    }
}
