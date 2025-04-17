#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Optimization;
using AIRefactored.Core;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Monitors bot threat conditions such as panic, multi-targeting, or squad losses.
    /// Dynamically triggers AI tuning for recoil, vision, aggression, and detection distance.
    /// </summary>
    public class BotThreatEscalationMonitor : MonoBehaviour
    {
        #region Fields

        private BotOwner _bot = null!;
        private float _lastCheckTime = 0f;
        private bool _hasEscalated = false;

        private const float CheckInterval = 1.0f;
        private const float PanicDurationThreshold = 4.0f;
        private float _panicStartTime = -1f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
        }

        private void Update()
        {
            if (_hasEscalated || Time.time < _lastCheckTime || _bot.IsDead)
                return;

            if (_bot.GetPlayer != null && !_bot.GetPlayer.IsAI)
                return; // 🛑 Skip real players or FIKA clients

            _lastCheckTime = Time.time + CheckInterval;

            if (ShouldEscalate())
            {
                EscalateBot();
            }
        }

        #endregion

        #region External Triggers

        /// <summary>
        /// Called externally by panic systems to begin monitoring escalation.
        /// </summary>
        public void NotifyPanicTriggered()
        {
            if (_panicStartTime < 0f)
                _panicStartTime = Time.time;
        }

        #endregion

        #region Escalation Trigger Logic

        /// <summary>
        /// Evaluates threat context and returns true if escalation should occur.
        /// </summary>
        private bool ShouldEscalate()
        {
            return PanicElapsed() || HasMultipleEnemies() || SquadHasCasualties();
        }

        /// <summary>
        /// True if bot has been in a panic state longer than threshold.
        /// </summary>
        private bool PanicElapsed()
        {
            return _panicStartTime > 0f && (Time.time - _panicStartTime) > PanicDurationThreshold;
        }

        /// <summary>
        /// True if bot is tracking two or more enemies.
        /// </summary>
        private bool HasMultipleEnemies()
        {
            var enemies = _bot.EnemiesController?.EnemyInfos;
            return enemies != null && enemies.Count >= 2;
        }

        /// <summary>
        /// True if enough squadmates have died to justify tactical escalation.
        /// </summary>
        private bool SquadHasCasualties()
        {
            var group = _bot.BotsGroup;
            if (group == null || group.MembersCount <= 1)
                return false;

            int dead = 0;
            for (int i = 0; i < group.MembersCount; i++)
            {
                var member = group.Member(i);
                if (member == null || member.IsDead)
                    dead++;
            }

            return dead >= Mathf.CeilToInt(group.MembersCount * 0.4f);
        }

        #endregion

        #region Escalation Execution

        /// <summary>
        /// Applies runtime tuning changes for vision, aggression, and weapon control.
        /// </summary>
        private void EscalateBot()
        {
            _hasEscalated = true;

            Debug.Log($"[AIRefactored-ThreatEscalation] Bot {_bot.Profile?.Info?.Nickname} escalating AI tuning.");

            AIOptimizationManager.Reset(_bot);
            AIOptimizationManager.Apply(_bot);

            ApplyEscalationTuning();
        }

        /// <summary>
        /// Applies internal tuning overrides for perception and combat response.
        /// </summary>
        private void ApplyEscalationTuning()
        {
            var settings = _bot.Settings?.FileSettings;
            if (settings == null)
                return;

            var mind = settings.Mind;
            var look = settings.Look;
            var shoot = settings.Shoot;

            if (shoot != null)
            {
                shoot.RECOIL_PER_METER = Mathf.Clamp(shoot.RECOIL_PER_METER * 0.85f, 0.1f, 2.0f);
            }

            if (mind != null)
            {
                mind.DIST_TO_FOUND_SQRT = Mathf.Clamp(mind.DIST_TO_FOUND_SQRT * 1.25f, 200f, 800f);
                mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(mind.ENEMY_LOOK_AT_ME_ANG * 0.7f, 5f, 30f);
                mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 = Mathf.Clamp(mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 + 25f, 0f, 100f);
            }

            if (look != null)
            {
                look.MAX_VISION_GRASS_METERS = Mathf.Clamp(look.MAX_VISION_GRASS_METERS + 5f, 5f, 40f);
            }

            Debug.Log($"[AIRefactored-Tuning] {_bot.Profile?.Info?.Nickname} → runtime recoil, vision, and sprint tuning escalated.");
        }

        #endregion
    }
}
