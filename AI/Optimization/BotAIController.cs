#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Behavior;
using AIRefactored.AI.Missions;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Combat;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Central AIRefactored controller: dispatches perception, behavior, mission, panic, suppression, and optimization.
    /// </summary>
    public class BotAIController : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotBehaviorEnhancer? _behavior;
        private BotMissionSystem? _mission;
        private BotThreatEscalationMonitor? _escalation;
        private BotTacticalDeviceController? _tactical;

        private float _nextUpdateTime;
        private const float UpdateInterval = 0.066f; // 66ms = ~15 ticks/sec

        public BotMissionSystem.MissionType? ForcedMissionType { get; set; }

        #endregion

        #region Initialization

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();

            // Skip entirely if this is a player (SPT/FIKA Coop)
            if (_bot?.GetPlayer is { IsYourPlayer: true })
            {
                enabled = false;
                return;
            }

            if (_bot?.GetPlayer != null)
            {
                var obj = _bot.GetPlayer.gameObject;

                _behavior = obj.GetComponent<BotBehaviorEnhancer>() ?? obj.AddComponent<BotBehaviorEnhancer>();
                _behavior.Init(_bot);

                _mission = obj.GetComponent<BotMissionSystem>() ?? obj.AddComponent<BotMissionSystem>();
                _mission.Init(_bot);

                if (ForcedMissionType.HasValue)
                    _mission.SetForcedMission(ForcedMissionType.Value);

                _escalation = obj.GetComponent<BotThreatEscalationMonitor>() ?? obj.AddComponent<BotThreatEscalationMonitor>();
                _tactical = obj.GetComponent<BotTacticalDeviceController>() ?? obj.AddComponent<BotTacticalDeviceController>();
            }

            TryOverrideMissionBasedOnMap();
        }

        #endregion

        #region AI Tick

        private void Update()
        {
            if (_bot == null || _cache == null || _bot.IsDead)
                return;

            float now = Time.time;
            if (now < _nextUpdateTime)
            {
                _tactical?.UpdateTacticalLogic(_bot, _cache);
                return;
            }

            _nextUpdateTime = now + UpdateInterval;

            HandleNightVision();
            HandleFlashExposure();
            HandleSuppressionReaction();

            if (_bot.Memory?.GoalEnemy != null)
            {
                TickCombatBehavior();
            }
            else if (_cache.LastHeardTime + 4f > now)
            {
                TickStealthBehavior();
            }
            else
            {
                TickPatrolBehavior();
            }

            _behavior?.Tick(now);
            _tactical?.UpdateTacticalLogic(_bot, _cache);
        }

        #endregion

        #region Flashlight / Suppression

        private void HandleFlashExposure()
        {
            if (!TryGetHeadTransform(out Transform? botHead) || botHead == null)
                return;

            foreach (Light light in FlashlightRegistry.GetActiveFlashlights())
            {
                if (!FlashLightUtils.IsBlindingLight(light.transform, botHead))
                    continue;

                float intensity = FlashLightUtils.GetFlashIntensityFactor(light.transform, botHead);
                float blindDuration = Mathf.Lerp(2f, 5f, intensity);

                _cache!.FlashGrenade?.AddBlindEffect(blindDuration, light.transform.position);
                _cache.IsBlinded = true;
                _cache.LastFlashTime = Time.time;

                if (BotPanicUtility.TryGet(_cache, out var panic))
                {
                    panic.TriggerPanic();
                    _escalation?.NotifyPanicTriggered();
                }

                if (_cache.TryGetComponent(out BotFlashReactionComponent? reaction))
                    reaction.TriggerSuppression();

                break;
            }
        }

        private void HandleSuppressionReaction()
        {
            if (_cache?.Suppression == null || _bot == null)
                return;

            if (Random.Range(0f, 1f) < 0.1f)
                _cache.Suppression.TriggerSuppression();
        }

        #endregion

        #region Behavior Phases

        private void TickCombatBehavior()
        {
            if (_bot?.Memory == null) return;

            _bot.Memory.AttackImmediately = true;

            if (_bot.Memory.GoalEnemy != null && !_bot.Memory.GoalEnemy.IsVisible)
                _bot.Memory.LoseVisionCurrentEnemy();
        }

        private void TickPatrolBehavior()
        {
            if (_bot?.Memory == null) return;

            _bot.Memory.AttackImmediately = false;
            _bot.Memory.IsPeace = true;
            _bot.Memory.CheckIsPeace();
        }

        private void TickStealthBehavior()
        {
            if (_bot?.Memory == null) return;

            _bot.Memory.AttackImmediately = false;
            _bot.Memory.IsPeace = false;

            if (_bot.Memory.GoalEnemy == null)
                _bot.Memory.LoseVisionCurrentEnemy();

            _bot.Memory.SetLastTimeSeeEnemy();
            _bot.Memory.CheckIsPeace();
        }

        #endregion

        #region Utility

        private bool TryGetHeadTransform(out Transform? head)
        {
            head = null;
            if (_cache == null)
                return false;

            head = BotCacheUtility.Head(_cache);
            return head != null;
        }

        private void HandleNightVision()
        {
            float ambient = RenderSettings.ambientLight.grayscale;
            if (ambient < 0.1f && _bot?.LookSensor != null)
            {
                _bot.LookSensor.ClearVisibleDist *= 1.2f;
            }
        }

        private void TryOverrideMissionBasedOnMap()
        {
            if (_mission == null || _bot == null || _cache?.AIRefactoredBotOwner?.PersonalityProfile == null)
                return;

            string map = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.ToLowerInvariant();
            var profile = _cache.AIRefactoredBotOwner.PersonalityProfile;

            float lootBias = 1.0f, fightBias = 1.0f, questBias = 1.0f;

            switch (map)
            {
                case "factory4_day":
                case "factory4_night":
                case "laboratory":
                case "sandbox":
                case "tarkovstreets":
                    fightBias += 1.5f;
                    break;
                case "interchange":
                case "lighthouse":
                case "rezervbase":
                case "groundzero":
                case "sandbox_high":
                    lootBias += 1.3f;
                    break;
                case "bigmap":
                case "shoreline":
                case "woods":
                    questBias += 1.2f;
                    fightBias += 0.4f;
                    break;
                default:
                    lootBias += 0.5f;
                    break;
            }

            switch (profile.Personality)
            {
                case PersonalityType.Adaptive:
                    lootBias += 0.4f;
                    fightBias += 0.4f;
                    questBias += 0.4f;
                    break;
                case PersonalityType.Aggressive:
                    fightBias += 2.0f;
                    questBias -= 0.3f;
                    break;
                case PersonalityType.Cautious:
                    lootBias += 1.2f;
                    questBias += 0.8f;
                    fightBias -= 0.6f;
                    break;
                case PersonalityType.Defensive:
                    lootBias += 1.0f;
                    fightBias += 0.4f;
                    break;
                case PersonalityType.Dumb:
                    fightBias += 1.0f;
                    questBias -= 0.4f;
                    break;
                case PersonalityType.Strategic:
                    questBias += 1.5f;
                    break;
                case PersonalityType.Frenzied:
                    fightBias += 2.5f;
                    lootBias -= 0.5f;
                    break;
                case PersonalityType.Fearful:
                    lootBias += 1.5f;
                    fightBias -= 1.0f;
                    break;
                case PersonalityType.Camper:
                    questBias += 1.2f;
                    lootBias += 0.6f;
                    break;
            }

            lootBias += profile.Caution * 0.8f;
            fightBias += profile.AggressionLevel * 1.5f;
            questBias += profile.Caution * 0.6f;

            if (profile.IsFrenzied) fightBias += 1.5f;
            if (profile.IsFearful) lootBias += 1.2f;
            if (profile.IsCamper) questBias += 1.0f;
            if (profile.IsStubborn) fightBias += 0.8f;
            if (profile.ChaosFactor > 0.6f) fightBias += profile.ChaosFactor;

            float total = lootBias + fightBias + questBias;
            float roll = Random.Range(0f, total);

            if (roll < lootBias)
                _mission.SetForcedMission(BotMissionSystem.MissionType.Loot);
            else if (roll < lootBias + fightBias)
                _mission.SetForcedMission(BotMissionSystem.MissionType.Fight);
            else
                _mission.SetForcedMission(BotMissionSystem.MissionType.Quest);
        }

        #endregion
    }
}
