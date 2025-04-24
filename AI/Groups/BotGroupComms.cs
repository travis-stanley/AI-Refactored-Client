#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Controls squad VO communication: fallback shouts, frag calls, suppression alerts, and injury.
    /// Uses cooldown, proximity checks, and chance modifiers to sound natural.
    /// </summary>
    public sealed class BotGroupComms
    {
        #region Constants

        private const float VoiceCooldown = 4.5f;
        private const float AllyRadius = 12f;
        private static readonly float AllyRadiusSq = AllyRadius * AllyRadius;

        #endregion

        #region Fields

        private readonly BotComponentCache _cache;
        private float _nextVoiceTime;

        #endregion

        #region Constructor

        public BotGroupComms(BotComponentCache cache)
        {
            _cache = cache;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Explicitly triggers a voice line, bypassing cooldowns and squad checks.
        /// </summary>
        public void Say(EPhraseTrigger phrase)
        {
            if (IsEligible())
                _cache.Bot?.BotTalk?.TrySay(phrase);
        }

        #endregion

        #region Voice Events

        /// <summary>
        /// Shouts "frag out" when allies are nearby (80% chance).
        /// </summary>
        public void SayFragOut()
        {
            TriggerVoice(EPhraseTrigger.OnEnemyGrenade, HasNearbyAlly() ? 0.8f : 0f);
        }

        /// <summary>
        /// Suppression alert VO (60% chance).
        /// </summary>
        public void SaySuppression()
        {
            TriggerVoice(EPhraseTrigger.Suppress, 0.6f);
        }

        /// <summary>
        /// Injury reaction VO (70% chance).
        /// </summary>
        public void SayHit()
        {
            TriggerVoice(EPhraseTrigger.OnBeingHurt, 0.7f);
        }

        /// <summary>
        /// "Get back!" fallback VO (50% chance).
        /// </summary>
        public void SayFallback()
        {
            TriggerVoice(EPhraseTrigger.GetBack, 0.5f);
        }

        #endregion

        #region Voice Helper

        private void TriggerVoice(EPhraseTrigger phrase, float chance = 1f)
        {
            if (!IsEligible())
                return;

            float now = Time.time;
            if (now < _nextVoiceTime)
                return;

            if (chance < 1f && UnityEngine.Random.value > chance)
                return;

            _nextVoiceTime = now + VoiceCooldown * UnityEngine.Random.Range(0.8f, 1.2f);
            _cache.Bot?.BotTalk?.TrySay(phrase);
        }

        private bool IsEligible()
        {
            var bot = _cache.Bot;
            return bot != null && bot.GetPlayer?.IsAI == true && !bot.IsDead;
        }

        private bool HasNearbyAlly()
        {
            var self = _cache.Bot;
            if (self == null)
                return false;

            string? groupId = self.Profile?.Info?.GroupId;
            if (string.IsNullOrEmpty(groupId))
                return false;

            Vector3 myPos = self.Position;

            foreach (var other in BotCacheUtility.AllActiveBots())
            {
                if (other == null || other == _cache)
                    continue;

                var mate = other.Bot;
                if (mate == null || mate.IsDead)
                    continue;

                string? otherGroup = mate.Profile?.Info?.GroupId;
                if (string.IsNullOrEmpty(otherGroup) || otherGroup != groupId)
                    continue;

                if ((mate.Position - myPos).sqrMagnitude <= AllyRadiusSq)
                    return true;
            }

            return false;
        }

        #endregion
    }
}
