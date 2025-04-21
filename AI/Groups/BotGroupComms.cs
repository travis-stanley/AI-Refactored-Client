#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.Runtime;
using BepInEx.Logging;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Controls squad communication behavior such as voice lines and fallback calls.
    /// Uses EFT's EPhraseTrigger to trigger in-game bot speech, with cooldown and range awareness.
    /// </summary>
    public class BotGroupComms
    {
        #region Fields

        private readonly BotComponentCache _cache;
        private float _nextVoiceTime = 0f;

        private const float VoiceCooldown = 4.5f;
        private const float NearbyAllyRadius = 12f;

        private static readonly ManualLogSource Logger = AIRefactoredController.Logger;

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs group communication logic for a bot.
        /// </summary>
        public BotGroupComms(BotComponentCache cache)
        {
            _cache = cache;
        }

        #endregion

        #region Voice Methods

        /// <summary>
        /// Sends an in-game voice line using the specified EFT phrase trigger.
        /// </summary>
        /// <param name="trigger">Voice trigger to play.</param>
        public void Say(EPhraseTrigger trigger)
        {
            if (Time.time < _nextVoiceTime || !IsValid())
                return;

            _nextVoiceTime = Time.time + VoiceCooldown;

            var talk = _cache.Bot?.BotTalk;
            if (talk != null)
            {
                talk.TrySay(trigger);
                Logger.LogInfo($"[Comms] {_cache.Bot?.Profile?.Info?.Nickname ?? "Unknown"}: {trigger}");
            }
        }

        /// <summary>
        /// Triggers 'Frag out!' VO if nearby teammates can hear it.
        /// </summary>
        public void SayFragOut()
        {
            if (IsValid() && IsFriendlyNearby())
                Say(EPhraseTrigger.OnEnemyGrenade);
        }

        /// <summary>
        /// Triggers 'Suppressing!' or equivalent VO line.
        /// </summary>
        public void SaySuppression()
        {
            if (IsValid())
                Say(EPhraseTrigger.Suppress);
        }

        /// <summary>
        /// Triggers 'I’m hit!' VO line.
        /// </summary>
        public void SayHit()
        {
            if (IsValid())
                Say(EPhraseTrigger.OnBeingHurt);
        }

        /// <summary>
        /// Triggers 'Falling back!' VO line.
        /// </summary>
        public void SayFallback()
        {
            if (IsValid())
                Say(EPhraseTrigger.GetBack);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns true if a friendly bot is nearby and alive (within radius).
        /// </summary>
        private bool IsFriendlyNearby()
        {
            if (_cache.Bot == null || _cache.Bot.IsDead)
                return false;

            Vector3 myPos = _cache.Position;
            string? myGroup = _cache.Bot.Profile?.Info?.GroupId;

            foreach (var other in BotCacheUtility.AllActiveBots())
            {
                if (other == _cache || other.Bot == null || other.Bot.IsDead)
                    continue;

                if (other.Bot.Profile?.Info?.GroupId != myGroup)
                    continue;

                if (Vector3.Distance(myPos, other.Position) < NearbyAllyRadius)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Confirms the bot is valid, alive, and under AIRefactored control.
        /// </summary>
        private bool IsValid()
        {
            return _cache.Bot != null &&
                   _cache.Bot.GetPlayer != null &&
                   _cache.Bot.GetPlayer.IsAI &&
                   !_cache.Bot.IsDead;
        }

        #endregion
    }
}
