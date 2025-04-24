#nullable enable

using AIRefactored.AI.Optimization;
using AIRefactored.Runtime;
using BepInEx.Logging;
using EFT;
using System;
using UnityEngine;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Tracks panic events, visible enemies, and squad losses to trigger escalation behavior.
    /// Applies tuning changes and personality adaptations based on threat severity.
    /// </summary>
    public class BotThreatEscalationMonitor
    {
        #region Fields

        private BotOwner _bot = null!;
        private bool _hasEscalated;

        private float _panicStartTime = -1f;
        private float _lastCheckTime = -1f;

        private const float CheckInterval = 1.0f;
        private const float PanicDurationThreshold = 4.0f;
        private const float SquadCasualtyThreshold = 0.4f;
        private const float SquadRadiusMeters = 15f;
        private static readonly float SquadRadiusSqr = SquadRadiusMeters * SquadRadiusMeters;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Initialization

        /// <summary>
        /// Links this monitor to a bot. Must be called once before Tick.
        /// </summary>
        public void Initialize(BotOwner bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        }

        #endregion

        #region Tick

        /// <summary>
        /// Called regularly to evaluate escalation conditions.
        /// </summary>
        public void Tick(float time)
        {
            if (_hasEscalated || !IsValid())
                return;

            if (time < _lastCheckTime)
                return;

            _lastCheckTime = time + CheckInterval;

            if (ShouldEscalate(time))
                EscalateBot();
        }

        #endregion

        #region Panic Notification

        public void NotifyPanicTriggered()
        {
            if (_panicStartTime < 0f)
                _panicStartTime = Time.time;
        }

        #endregion

        #region Escalation Evaluation

        private bool ShouldEscalate(float time)
        {
            return PanicDurationExceeded(time)
                || MultipleEnemiesVisible()
                || SquadHasLostTeammates();
        }

        private bool PanicDurationExceeded(float time)
        {
            return _panicStartTime >= 0f && (time - _panicStartTime) > PanicDurationThreshold;
        }

        private bool MultipleEnemiesVisible()
        {
            var infos = _bot.EnemiesController?.EnemyInfos;
            return infos != null && infos.Count >= 2;
        }

        private bool SquadHasLostTeammates()
        {
            var group = _bot.BotsGroup;
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

        private void EscalateBot()
        {
            _hasEscalated = true;

            string name = _bot.Profile?.Info?.Nickname ?? "Unknown";
            Logger.LogInfo($"[AIRefactored-Escalation] Escalating behavior for bot '{name}'.");

            AIOptimizationManager.Reset(_bot);
            AIOptimizationManager.Apply(_bot);

            ApplyEscalationTuning();
            ApplyPersonalityTuning();
        }

        private void ApplyEscalationTuning()
        {
            var settings = _bot.Settings?.FileSettings;
            if (settings == null)
                return;

            var shoot = settings.Shoot;
            if (shoot != null)
                shoot.RECOIL_PER_METER = Mathf.Clamp(shoot.RECOIL_PER_METER * 0.85f, 0.1f, 2f);

            var mind = settings.Mind;
            if (mind != null)
            {
                mind.DIST_TO_FOUND_SQRT = Mathf.Clamp(mind.DIST_TO_FOUND_SQRT * 1.2f, 200f, 800f);
                mind.ENEMY_LOOK_AT_ME_ANG = Mathf.Clamp(mind.ENEMY_LOOK_AT_ME_ANG * 0.75f, 5f, 45f);
                mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 = Mathf.Clamp(
                    mind.CHANCE_TO_RUN_CAUSE_DAMAGE_0_100 + 20f, 0f, 100f);
            }

            var look = settings.Look;
            if (look != null)
                look.MAX_VISION_GRASS_METERS = Mathf.Clamp(look.MAX_VISION_GRASS_METERS + 5f, 5f, 40f);

            Logger.LogInfo($"[AIRefactored-Tuning] Escalation tuning applied to {_bot.Profile?.Info?.Nickname ?? "Unknown"}.");
        }

        private void ApplyPersonalityTuning()
        {
            var prof = BotRegistry.Get(_bot.ProfileId);
            if (prof == null)
                return;

            prof.AggressionLevel = Mathf.Clamp01(prof.AggressionLevel + 0.25f);
            prof.Caution = Mathf.Clamp01(prof.Caution - 0.25f);
            prof.SuppressionSensitivity = Mathf.Clamp01(prof.SuppressionSensitivity * 0.75f);
            prof.AccuracyUnderFire = Mathf.Clamp01(prof.AccuracyUnderFire + 0.2f);
            prof.CommunicationLevel = Mathf.Clamp01(prof.CommunicationLevel + 0.2f);

            string name = _bot.Profile?.Info?.Nickname ?? "Unknown";
            Logger.LogInfo(
                $"[AIRefactored-Tuning] Personality tuned for {name}: " +
                $"Agg={prof.AggressionLevel:F2}, Caution={prof.Caution:F2}, " +
                $"Supp={prof.SuppressionSensitivity:F2}, UnderFireAcc={prof.AccuracyUnderFire:F2}"
            );
        }

        #endregion

        #region Validation

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
