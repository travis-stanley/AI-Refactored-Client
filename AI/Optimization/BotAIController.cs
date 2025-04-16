#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Behavior;
using AIRefactored.AI.Missions;
using System.Collections.Generic;

namespace AIRefactored.AI.Optimization
{
    public class BotAIController : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotBehaviorEnhancer? _behavior;
        private BotMissionSystem? _mission;

        private float _nextUpdateTime;
        private const float UpdateInterval = 0.25f;

        public BotMissionSystem.MissionType? ForcedMissionType { get; set; }

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();

            if (_cache != null)
            {
                BotAIManager.Register(_cache);
#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-AI] Registered controller for {_bot?.Profile?.Info?.Nickname ?? "?"}");
#endif
            }

            if (_bot?.GetPlayer != null)
            {
                var playerObj = _bot.GetPlayer.gameObject;

                _behavior = playerObj.AddComponent<BotBehaviorEnhancer>();
                _behavior.Init(_bot);

                _mission = playerObj.AddComponent<BotMissionSystem>();
                if (ForcedMissionType.HasValue)
                    _mission.SetForcedMission(ForcedMissionType.Value);

                _mission.Init(_bot);
            }
        }

        private void OnDestroy()
        {
            if (_cache != null)
            {
                BotAIManager.Unregister(_cache);
#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-AI] Unregistered controller for {_bot?.Profile?.Info?.Nickname ?? "?"}");
#endif
            }
        }

        public void TickAI(float currentTime)
        {
            if (_bot == null || _cache == null || currentTime < _nextUpdateTime)
                return;

            _nextUpdateTime = currentTime + UpdateInterval;

            HandleLightThreats();
            HandleSuppressionReaction();
            _behavior?.Tick(currentTime);
        }

        private void HandleLightThreats()
        {
            if (!TryGetHeadTransform(out var botHead))
                return;

            List<Light> lights = new List<Light>();
            foreach (var l in FlashlightRegistry.GetActiveFlashlights())
            {
                lights.Add(l);
            }

            int count = lights.Count;
            for (int i = 0; i < count; i++)
            {
                Light light = lights[i];
                if (!FlashLightUtils.IsBlindingLight(light.transform, botHead))
                    continue;

                float intensity = FlashLightUtils.GetFlashIntensityFactor(light.transform, botHead);
                float blindDuration = Mathf.Lerp(2f, 5f, intensity);

                _cache.FlashGrenade?.AddBlindEffect(blindDuration, light.transform.position);

                if (BotPanicUtility.TryGet(_cache, out var panic))
                    panic.TriggerPanic();

                if (_cache.TryGetComponent(out BotFlashReactionComponent? reaction))
                    reaction.TriggerSuppression();

#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-AI] {_bot?.Profile?.Info?.Nickname ?? "?"} blinded. Intensity={intensity:F2}");
#endif
                break; // Only process one light per tick
            }
        }

        private void HandleSuppressionReaction()
        {
            if (_cache?.Suppression == null || _bot == null)
                return;

            if (UnityEngine.Random.Range(0f, 1f) < 0.1f)
            {
                _cache.Suppression.TriggerSuppression();
#if UNITY_EDITOR
                Debug.Log($"[AIRefactored-AI] {_bot?.Profile?.Info?.Nickname ?? "?"} suppression triggered.");
#endif
            }
        }

        private bool TryGetHeadTransform(out Transform head)
        {
            head = default!;
            if (_cache == null)
                return false;

            head = BotCacheUtility.Head(_cache);
            return head != null;
        }
    }
}
