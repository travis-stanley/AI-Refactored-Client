#nullable enable

using UnityEngine;
using EFT;
using Comfort.Common;
using AIRefactored.AI.Core;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Perception;

namespace AIRefactored.AI.Combat
{
    /// <summary>
    /// Handles panic behavior for bots. Triggers a retreat/fallback when blinded or under high threat.
    /// </summary>
    public class BotPanicHandler : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;

        private float _panicStartTime = -1f;
        private const float PanicDuration = 3.5f;
        private bool _isPanicking = false;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
        }

        private void Update()
        {
            if (_bot == null || _cache == null)
                return;

            if (ShouldTriggerPanic() && !_isPanicking)
            {
                StartPanic();
            }

            if (_isPanicking && Time.time - _panicStartTime > PanicDuration)
            {
                EndPanic();
            }
        }

        public void TriggerPanic()
        {
            if (!_isPanicking)
                StartPanic();
        }

        private bool ShouldTriggerPanic()
        {
            if (_cache?.FlashGrenade?.IsFlashed() == true)
                return true;

            var hp = _bot.HealthController.GetBodyPartHealth(EBodyPart.Common);
            return hp.Current < 25f;
        }

        private void StartPanic()
        {
            _isPanicking = true;
            _panicStartTime = Time.time;

            Vector3 fallbackDir = -_bot.LookDirection.normalized;
            Vector3 fallbackPos = _bot.Position + fallbackDir * 8f;

            if (Physics.Raycast(_bot.Position, fallbackDir, out var hit, 8f))
            {
                fallbackPos = hit.point - fallbackDir * 1f;
            }

            _bot.FallbackTo(fallbackPos);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);

            string mapId = Singleton<GameWorld>.Instance?.MainPlayer?.Location?.ToLowerInvariant() ?? "unknown";
            BotMemoryStore.AddDangerZone(mapId, _bot.Position, DangerTriggerType.Panic, 0.6f);

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored-Panic] Bot {_bot.Profile?.Info?.Nickname ?? "?"} entered PANIC → fallback to {fallbackPos}");
#endif
        }

        private void EndPanic()
        {
            _isPanicking = false;
            _bot?.RestoreCombatAggression();

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored-Panic] Bot {_bot.Profile?.Info?.Nickname ?? "?"} exited PANIC state.");
#endif
        }
    }
}
