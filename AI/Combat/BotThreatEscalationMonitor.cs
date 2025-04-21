#nullable enable

using AIRefactored.AI.Optimization;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Monitors panic duration, enemy count, and squad losses to trigger threat escalation.
    /// Invoked via BotBrain to dynamically adjust bot tuning and combat posture.
    /// </summary>
    public class BotThreatEscalationMonitor
    {
        #region Fields

        private BotOwner? _bot;
        private float _panicStartTime = -1f;
        private float _lastCheckTime = -1f;
        private bool _hasEscalated = false;

        private const float CheckInterval = 1.0f;
        private const float PanicDurationThreshold = 4.0f;
        private const float SquadCasualtyThreshold = 0.4f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Initializes the monitor with the bot instance.
        /// </summary>
        /// <param name="bot">Bot owner instance to track.</param>
        public void Initialize(BotOwner bot)
        {
            _bot = bot;
        }

        #endregion

        #region Tick Logic

        /// <summary>
        /// Main update loop. Evaluates whether escalation should be triggered.
        /// </summary>
        /// <param name="time">Current world time (Time.time).</param>
        public void Tick(float time)
        {
            if (_hasEscalated || !IsValid())
                return;

            if (time < _lastCheckTime)
                return;

            _lastCheckTime = time + CheckInterval;

            if (ShouldEscalate())
            {
                EscalateBot();
            }
        }

        /// <summary>
        /// Notifies the monitor that a panic event has occurred.
        /// Used to track panic duration.
        /// </summary>
        public void NotifyPanicTriggered()
        {
            if (_panicStartTime < 0f)
                _panicStartTime = Time.time;
        }

        #endregion

        #region Evaluation Checks

        /// <summary>
        /// Determines if escalation conditions are met.
        /// </summary>
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
            if (group == null)
                return false;

            int total = group.MembersCount;
            if (total <= 1)
                return false;

            int dead = 0;
            for (int i = 0; i < total; i++)
            {
                var member = group.Member(i);
                if (member == null || member.IsDead)
                    dead++;
            }

            return dead >= Mathf.CeilToInt(total * SquadCasualtyThreshold);
        }

        #endregion

        #region Escalation Logic

        /// <summary>
        /// Escalates bot tuning and personality once trigger conditions are met.
        /// </summary>
        private void EscalateBot()
        {
            if (_bot == null)
                return;

            _hasEscalated = true;

            string name = _bot.Profile?.Info?.Nickname ?? "Unknown";
            Logger.LogInfo($"[AIRefactored-Escalation] 🔺 Bot {name} is escalating behavior due to threat conditions.");

            // Reset and re-apply optimizations
            AIOptimizationManager.Reset(_bot);
            AIOptimizationManager.Apply(_bot);

            // Adjust internal tuning
            ApplyEscalationTuning();
            ApplyPersonalityTuning();
        }

        /// <summary>
        /// Applies internal EFT bot parameter tuning for escalation.
        /// </summary>
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

            Logger.LogInfo($"[AIRefactored-Tuning] ✅ Escalation tuning applied to {_bot.Profile?.Info?.Nickname ?? "Unknown"}.");
        }

        /// <summary>
        /// Adjusts the bot's personality profile to reflect a more aggressive stance.
        /// </summary>
        private void ApplyPersonalityTuning()
        {
            if (_bot == null || _bot.Profile == null)
                return;

            var profile = BotRegistry.Get(_bot.ProfileId);
            if (profile == null)
                return;

            profile.AggressionLevel = Mathf.Clamp01(profile.AggressionLevel + 0.25f);
            profile.Caution = Mathf.Clamp01(profile.Caution - 0.25f);
            profile.SuppressionSensitivity = Mathf.Clamp01(profile.SuppressionSensitivity * 0.75f);
            profile.AccuracyUnderFire = Mathf.Clamp01(profile.AccuracyUnderFire + 0.2f);
            profile.CommunicationLevel = Mathf.Clamp01(profile.CommunicationLevel + 0.2f);

            Logger.LogInfo($"[AIRefactored-Tuning] 🔥 Personality escalation applied to {_bot.Profile.Info.Nickname}: " +
                           $"Aggression={profile.AggressionLevel:F2}, Caution={profile.Caution:F2}, " +
                           $"Suppression={profile.SuppressionSensitivity:F2}, AccuracyUnderFire={profile.AccuracyUnderFire:F2}");
        }

        #endregion

        #region Validation

        /// <summary>
        /// Ensures bot is valid, alive, and AI-controlled.
        /// </summary>
        private bool IsValid()
        {
            return _bot != null &&
                   !_bot.IsDead &&
                   _bot.GetPlayer != null &&
                   _bot.GetPlayer.IsAI;
        }

        #endregion
    }
}
