#nullable enable

using AIRefactored.AI.Optimization;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Monitors conditions like panic duration, enemy count, and squad losses to trigger threat escalation.
    /// Used by BotBrain to boost bot reaction and tuning dynamically.
    /// </summary>
    public class BotThreatEscalationMonitor : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private float _lastCheckTime = 0f;
        private float _panicStartTime = -1f;
        private bool _hasEscalated = false;

        private const float CheckInterval = 1.0f;
        private const float PanicDurationThreshold = 4.0f;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            if (_bot == null)
                Debug.LogError("[AIRefactored] ❌ BotThreatEscalationMonitor missing BotOwner!");
        }

        #endregion

        #region Public API

        /// <summary>
        /// Tick handler for BotBrain. Executes all escalation logic.
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
        /// Records panic start time. Can be called externally from flash/suppression logic.
        /// </summary>
        public void NotifyPanicTriggered()
        {
            if (_panicStartTime < 0f)
                _panicStartTime = Time.time;
        }

        #endregion

        #region Core Logic

        private bool ShouldEscalate()
        {
            return PanicDurationExceeded() || HasMultipleEnemies() || SquadHasSustainedCasualties();
        }

        private bool PanicDurationExceeded()
        {
            return _panicStartTime > 0f && (Time.time - _panicStartTime) > PanicDurationThreshold;
        }

        private bool HasMultipleEnemies()
        {
            var enemies = _bot?.EnemiesController?.EnemyInfos;
            return enemies != null && enemies.Count >= 2;
        }

        private bool SquadHasSustainedCasualties()
        {
            var group = _bot?.BotsGroup;
            if (group == null || group.MembersCount <= 1)
                return false;

            int deadCount = 0;
            for (int i = 0; i < group.MembersCount; i++)
            {
                var member = group.Member(i);
                if (member == null || member.IsDead)
                    deadCount++;
            }

            return deadCount >= Mathf.CeilToInt(group.MembersCount * 0.4f);
        }

        private void EscalateBot()
        {
            if (_bot == null)
                return;

            _hasEscalated = true;

            string name = _bot.Profile?.Info?.Nickname ?? "Unknown";
            Debug.Log($"[AIRefactored-ThreatEscalation] Bot {name} is escalating tuning parameters.");

            AIOptimizationManager.Reset(_bot);
            AIOptimizationManager.Apply(_bot);

            ApplyEscalationTuning(name);
        }

        private void ApplyEscalationTuning(string botName)
        {
            if (_bot?.Settings?.FileSettings == null)
                return;

            var mind = _bot.Settings.FileSettings.Mind;
            var look = _bot.Settings.FileSettings.Look;
            var shoot = _bot.Settings.FileSettings.Shoot;

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

            Debug.Log($"[AIRefactored-Tuning] {botName} → Recoil, vision, and mind settings escalated at runtime.");
        }

        private bool IsValid()
        {
            return _bot != null &&
                   _bot.GetPlayer != null &&
                   _bot.GetPlayer.IsAI &&
                   !_bot.IsDead;
        }

        #endregion
    }
}
