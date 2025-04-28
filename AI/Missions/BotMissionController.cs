#nullable enable

namespace AIRefactored.AI.Missions
{
    using System;

    using AIRefactored.AI.Core;
    using AIRefactored.AI.Missions.Subsystems;

    using EFT;

    using UnityEngine;

    using Random = UnityEngine.Random;

    /// <summary>
    ///     Central runtime mission handler.
    ///     Coordinates bot objectives, mission switching, fallback, and extraction.
    ///     Modularized into subsystems for maintainability and realism.
    /// </summary>
    public sealed class BotMissionController
    {
        private readonly BotOwner _bot;

        private readonly BotComponentCache _cache;

        private readonly MissionEvaluator _evaluator;

        private readonly ObjectiveController _objectives;

        private readonly MissionSwitcher _switcher;

        private readonly MissionVoiceCoordinator _voice;

        private bool _forcedMission;

        private bool _inCombatPause;

        private float _lastCombatTime;

        private float _lastUpdate;

        private MissionType _missionType;

        private bool _waitForGroup;

        public BotMissionController(BotOwner bot, BotComponentCache cache)
        {
            this._bot = bot ?? throw new ArgumentNullException(nameof(bot));
            this._cache = cache ?? throw new ArgumentNullException(nameof(cache));

            this._switcher = new MissionSwitcher(bot, cache);
            this._objectives = new ObjectiveController(bot, cache);
            this._evaluator = new MissionEvaluator(bot, cache);
            this._voice = new MissionVoiceCoordinator(bot);

            this._missionType = this.PickDefaultMission();
            this._objectives.SetInitialObjective(this._missionType);

            // Reverse binding for helper access
            this._cache.AIRefactoredBotOwner?.SetMissionController(this);
        }

        public enum MissionType
        {
            Loot,

            Fight,

            Quest
        }

        private float _cooldown => 10f + Random.Range(0f, 5f);

        public void SetForcedMission(MissionType mission)
        {
            this._missionType = mission;
            this._forcedMission = true;
        }

        public void Tick(float time)
        {
            if (this._bot.IsDead || !this._bot.GetPlayer?.HealthController?.IsAlive == true)
                return;

            if (this._cache.IsBlinded || this._cache.PanicHandler?.IsPanicking == true)
                return;

            this._evaluator.UpdateStuckCheck(time);

            this._switcher.Evaluate(
                ref this._missionType,
                time,
                this.SwitchToFight,
                this._objectives.ResumeQuesting,
                this._evaluator.IsGroupAligned);

            if (this._cache.Combat?.IsInCombatState() == true)
            {
                this._lastCombatTime = time;
                if (this._missionType == MissionType.Quest) this._inCombatPause = true;
            }

            if (this._inCombatPause && time - this._lastCombatTime > 4f)
            {
                this._inCombatPause = false;
                this._objectives.ResumeQuesting();
            }

            if (this._missionType is MissionType.Loot or MissionType.Quest && this._evaluator.ShouldExtractEarly())
            {
                this._evaluator.TryExtract();
                return;
            }

            if (this._waitForGroup && !this._evaluator.IsGroupAligned() && time - this._lastUpdate < 15f)
                return;

            if (time - this._lastUpdate > this._cooldown)
            {
                this._objectives.OnObjectiveReached(this._missionType);
                this._lastUpdate = time;
            }

            if (!this._inCombatPause && Vector3.Distance(this._bot.Position, this._objectives.CurrentObjective) < 6f)
                this.OnObjectiveReached();
        }

        private void OnObjectiveReached()
        {
            if (this._missionType == MissionType.Loot)
            {
                this._cache.Movement?.EnterLootingMode();
                this._cache.PoseController?.LockCrouchPose();
                this._cache.LootScanner?.TryLootNearby();
                this._cache.DeadBodyScanner?.TryLootNearby();
                this._cache.Movement?.ExitLootingMode();
                this._voice.OnLoot();
            }

            this._objectives.OnObjectiveReached(this._missionType);
        }

        private MissionType PickDefaultMission()
        {
            var isPmc = this._bot.Profile?.Info?.Side is EPlayerSide.Usec or EPlayerSide.Bear;
            var personality = BotRegistry.TryGet(this._bot.ProfileId);

            return personality?.PreferredMission switch
                {
                    MissionBias.Quest => isPmc ? MissionType.Quest : MissionType.Loot,
                    MissionBias.Fight => MissionType.Fight,
                    MissionBias.Loot => MissionType.Loot,
                    _ => this.RandomizeDefault(isPmc)
                };
        }

        private MissionType RandomizeDefault(bool isPmc)
        {
            var roll = Random.Range(0, 100);
            if (roll < 30) return MissionType.Loot;
            if (roll < 65) return MissionType.Fight;
            return isPmc ? MissionType.Quest : MissionType.Loot;
        }

        private void SwitchToFight()
        {
            this._missionType = MissionType.Fight;
            this._objectives.SetInitialObjective(this._missionType);
            this._voice.OnMissionSwitch();
        }
    }
}