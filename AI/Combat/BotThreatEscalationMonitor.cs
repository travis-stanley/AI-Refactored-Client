#nullable enable

using AIRefactored.AI.Optimization;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Monitors panic duration, enemy count, and squad losses to trigger runtime threat escalation.
    /// Used by BotBrain to boost bot tuning and threat posture.
    /// </summary>
    public class BotThreatEscalationMonitor : MonoBehaviour
    {
        private BotOwner? _bot;
        private float _panicStartTime = -1f;
        private float _lastCheckTime = -1f;
        private bool _hasEscalated = false;

        private const float CheckInterval = 1.0f;
        private const float PanicDurationThreshold = 4.0f;
        private const float SquadCasualtyThreshold = 0.4f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            if (_bot == null)
                Debug.LogError("[AIRefactored] ❌ BotThreatEscalationMonitor missing BotOwner!");
        }

        /// <summary>
        /// Main tick entry, driven by BotBrain. Performs escalation checks.
        /// </summary>
        public void Tick(float time)
        {
            if (!IsValid() || _hasEscalated || time < _lastCheckTime)
                return;

            _lastCheckTime = time + CheckInterval;

            if (ShouldEscalate())
                EscalateBot();
        }

        /// <summary>
        /// Called externally when suppression or flash panic is triggered.
        /// </summary>
        public void NotifyPanicTriggered()
        {
            if (_panicStartTime < 0f)
                _panicStartTime = Time.time;
        }

        private bool ShouldEscalate()
        {
            return PanicDurationExceeded() || MultipleEnemiesVisible() || SquadHasLostTeammates();
        }

        private bool PanicDurationExceeded()
        {
            return _panicStartTime > 0f && (Time.time - _panicStartTime) > PanicDurationThreshold;
        }

        private bool MultipleEnemiesVisible()
        {
            var enemies = _bot?.EnemiesController?.EnemyInfos;
            return enemies != null && enemies.Count >= 2;
        }

        private bool SquadHasLostTeammates()
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

            return dead >= Mathf.CeilToInt(group.MembersCount * SquadCasualtyThreshold);
        }

        private void EscalateBot()
        {
            if (_bot == null || _hasEscalated)
                return;

            _hasEscalated = true;

            string name = _bot.Profile?.Info?.Nickname ?? "Unknown";
            Debug.Log($"[AIRefactored-Escalation] 🔺 Bot {name} is escalating behavior due to threat conditions.");

            // Re-apply base optimizations before boost
            AIOptimizationManager.Reset(_bot);
            AIOptimizationManager.Apply(_bot);

            ApplyEscalationTuning();
        }

        private void ApplyEscalationTuning()
        {
            if (_bot?.Settings?.FileSettings == null)
                return;

            var mind = _bot.Settings.FileSettings.Mind;
            var look = _bot.Settings.FileSettings.Look;
            var shoot = _bot.Settings.FileSettings.Shoot;

            if (shoot != null)
                shoot.RECOIL_PER_METER = Mathf.Clamp(shoot.RECOIL_PER_METER * 0.85f, 0.1f, 2f);

            if (mind != null)
            {
                mind.DIST_TO_FOUND_SQRT = Mathf.Clamp(mind.DIST_TO_FOUND_SQRT * 1.2f, 200f, 800f);
                mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(mind.ENEMY_LOOK_AT_ME_ANG * 0.75f, 5f, 45f);
                mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 = Mathf.Clamp(mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 + 20f, 0f, 100f);
            }

            if (look != null)
                look.MAX_VISION_GRASS_METERS = Mathf.Clamp(look.MAX_VISION_GRASS_METERS + 5f, 5f, 40f);

            Debug.Log($"[AIRefactored-Tuning] ✅ Escalation tuning applied to {_bot.Profile?.Info?.Nickname ?? "Unknown"}.");
        }

        private bool IsValid()
        {
            return _bot != null &&
                   _bot.GetPlayer != null &&
                   _bot.GetPlayer.IsAI &&
                   !_bot.IsDead;
        }
    }
}
