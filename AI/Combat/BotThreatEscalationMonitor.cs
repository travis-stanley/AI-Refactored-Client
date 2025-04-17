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

        private BotOwner? _bot;
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

            if (_bot == null)
                Debug.LogError("[AIRefactored] BotThreatEscalationMonitor missing BotOwner!");
        }

        private void Update()
        {
            if (_bot == null || _hasEscalated || Time.time < _lastCheckTime || _bot.IsDead)
                return;

            if (_bot.GetPlayer != null && !_bot.GetPlayer.IsAI)
                return;

            _lastCheckTime = Time.time + CheckInterval;

            if (ShouldEscalate())
            {
                EscalateBot();
            }
        }

        #endregion

        #region External Triggers

        public void NotifyPanicTriggered()
        {
            if (_bot == null)
                return;

            if (_panicStartTime < 0f)
                _panicStartTime = Time.time;
        }

        #endregion

        #region Escalation Trigger Logic

        private bool ShouldEscalate()
        {
            return _bot != null && (PanicElapsed() || HasMultipleEnemies() || SquadHasCasualties());
        }

        private bool PanicElapsed()
        {
            return _panicStartTime > 0f && (Time.time - _panicStartTime) > PanicDurationThreshold;
        }

        private bool HasMultipleEnemies()
        {
            var enemies = _bot?.EnemiesController?.EnemyInfos;
            return enemies != null && enemies.Count >= 2;
        }

        private bool SquadHasCasualties()
        {
            var group = _bot?.BotsGroup;
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

        private void EscalateBot()
        {
            if (_bot == null)
                return;

            _hasEscalated = true;

            Debug.Log($"[AIRefactored-ThreatEscalation] Bot {_bot.Profile?.Info?.Nickname ?? "Unknown"} escalating AI tuning.");

            AIOptimizationManager.Reset(_bot);
            AIOptimizationManager.Apply(_bot);

            ApplyEscalationTuning();
        }

        private void ApplyEscalationTuning()
        {
            if (_bot?.Settings?.FileSettings is not { } settings)
                return;

            var mind = settings.Mind;
            var look = settings.Look;
            var shoot = settings.Shoot;

            if (shoot != null)
                shoot.RECOIL_PER_METER = Mathf.Clamp(shoot.RECOIL_PER_METER * 0.85f, 0.1f, 2.0f);

            if (mind != null)
            {
                mind.DIST_TO_FOUND_SQRT = Mathf.Clamp(mind.DIST_TO_FOUND_SQRT * 1.25f, 200f, 800f);
                mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(mind.ENEMY_LOOK_AT_ME_ANG * 0.7f, 5f, 30f);
                mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 = Mathf.Clamp(mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 + 25f, 0f, 100f);
            }

            if (look != null)
                look.MAX_VISION_GRASS_METERS = Mathf.Clamp(look.MAX_VISION_GRASS_METERS + 5f, 5f, 40f);

            Debug.Log($"[AIRefactored-Tuning] {_bot?.Profile?.Info?.Nickname ?? "Unknown"} → runtime recoil, vision, and sprint tuning escalated.");
        }

        #endregion
    }
}
