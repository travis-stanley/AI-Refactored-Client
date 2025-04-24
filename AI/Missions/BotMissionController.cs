#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Missions.Subsystems;
using EFT;
using System;
using UnityEngine;

namespace AIRefactored.AI.Missions
{
    /// <summary>
    /// Central runtime mission handler.
    /// Coordinates bot objectives, mission switching, fallback, and extraction.
    /// Modularized into subsystems for maintainability and realism.
    /// </summary>
    public sealed class BotMissionController
    {
        public enum MissionType { Loot, Fight, Quest }

        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;

        private readonly MissionSwitcher _switcher;
        private readonly ObjectiveController _objectives;
        private readonly MissionEvaluator _evaluator;
        private readonly MissionVoiceCoordinator _voice;

        private MissionType _missionType;

        private float _lastCombatTime;
        private float _lastUpdate;
        private float _cooldown => 10f + UnityEngine.Random.Range(0f, 5f);

        private bool _inCombatPause;
        private bool _waitForGroup;
        private bool _forcedMission;

        public BotMissionController(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;

            _switcher = new MissionSwitcher(bot, cache);
            _objectives = new ObjectiveController(bot, cache);
            _evaluator = new MissionEvaluator(bot, cache);
            _voice = new MissionVoiceCoordinator(bot);

            _missionType = PickDefaultMission();
            _objectives.SetInitialObjective(_missionType);
        }

        public void SetForcedMission(MissionType mission)
        {
            _missionType = mission;
            _forcedMission = true;
        }

        public void Tick(float time)
        {
            if (_bot.IsDead || !_bot.GetPlayer?.HealthController?.IsAlive == true)
                return;

            if (_cache.IsBlinded || _cache.PanicHandler?.IsPanicking == true)
                return;

            _evaluator.UpdateStuckCheck(time);

            _switcher.Evaluate(
                ref _missionType,
                time,
                SwitchToFight,
                _objectives.ResumeQuesting,
                _evaluator.IsGroupAligned
            );

            if (_cache.Combat?.IsInCombatState() == true)
            {
                _lastCombatTime = time;
                if (_missionType == MissionType.Quest)
                    _inCombatPause = true;
            }

            if (_inCombatPause && time - _lastCombatTime > 4f)
            {
                _inCombatPause = false;
                _objectives.ResumeQuesting();
            }

            if (_missionType is MissionType.Loot or MissionType.Quest &&
                _evaluator.ShouldExtractEarly())
            {
                _evaluator.TryExtract();
                return;
            }

            if (_waitForGroup && !_evaluator.IsGroupAligned() && time - _lastUpdate < 15f)
                return;

            if (time - _lastUpdate > _cooldown)
            {
                _objectives.OnObjectiveReached(_missionType);
                _lastUpdate = time;
            }

            if (!_inCombatPause && Vector3.Distance(_bot.Position, _objectives.CurrentObjective) < 6f)
            {
                OnObjectiveReached();
            }
        }

        private void SwitchToFight()
        {
            _missionType = MissionType.Fight;
            _objectives.SetInitialObjective(_missionType);
            _voice.OnMissionSwitch();
        }

        private void OnObjectiveReached()
        {
            if (_missionType == MissionType.Loot)
            {
                _cache.Movement?.EnterLootingMode();
                _cache.PoseController?.LockCrouchPose();
                _cache.LootScanner?.TryLootNearby();
                _cache.DeadBodyScanner?.TryLootNearby();
                _cache.Movement?.ExitLootingMode();
                _voice.OnLoot();
            }

            _objectives.OnObjectiveReached(_missionType);
        }

        private MissionType PickDefaultMission()
        {
            bool isPmc = _bot.Profile.Info.Side is EPlayerSide.Usec or EPlayerSide.Bear;
            var personality = BotRegistry.Get(_bot.Profile.Id);

            return personality.PreferredMission switch
            {
                MissionBias.Quest => isPmc ? MissionType.Quest : MissionType.Loot,
                MissionBias.Fight => MissionType.Fight,
                MissionBias.Loot => MissionType.Loot,
                _ => RandomizeDefault(isPmc)
            };
        }

        private MissionType RandomizeDefault(bool isPmc)
        {
            int roll = UnityEngine.Random.Range(0, 100);
            if (roll < 30) return MissionType.Loot;
            if (roll < 65) return MissionType.Fight;
            return isPmc ? MissionType.Quest : MissionType.Loot;
        }
    }
}
