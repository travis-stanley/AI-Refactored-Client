#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.Runtime;
using BepInEx.Logging;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Handles squad communication triggers such as VO lines or tactical signals.
    /// </summary>
    public class BotGroupComms
    {
        private readonly BotComponentCache _cache;
        private float _nextVoiceTime = 0f;
        private const float VoiceCooldown = 5f;

        private static readonly ManualLogSource _log = AIRefactoredController.Logger;

        public BotGroupComms(BotComponentCache cache)
        {
            _cache = cache;
        }

        public void Say(string message)
        {
            if (Time.time < _nextVoiceTime)
                return;

            _nextVoiceTime = Time.time + VoiceCooldown;

            _log.LogInfo($"[Comms] {_cache.Bot?.Profile.Info.Nickname}: {message}");
        }

        public void SayFragOut()
        {
            if (IsFriendlyNearby())
                Say("Frag out!");
        }

        public void SaySuppression()
        {
            Say("Suppressing!");
        }

        public void SayHit()
        {
            Say("I’m hit!");
        }

        public void SayFallback()
        {
            Say("Falling back!");
        }

        private bool IsFriendlyNearby()
        {
            Vector3 myPos = _cache.Position;

            foreach (var bot in BotCacheUtility.AllActiveBots())
            {
                if (bot == _cache) continue;

                float dist = Vector3.Distance(myPos, bot.Position);
                if (dist < 12f)
                    return true;
            }

            return false;
        }
    }
}
