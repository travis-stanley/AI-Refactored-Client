#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Memory;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Controls crouching, standing, prone, and lean pose transitions based on combat state, suppression, flanks, and cover.
    /// </summary>
    public class BotPoseController
    {
        private readonly BotOwner _bot;
        private readonly BotComponentCache _cache;
        private readonly MovementContext _movement;
        private readonly BotPersonalityProfile _personality;

        private float _nextPoseCheck = 0f;
        private const float PoseCheckInterval = 0.35f;

        private const float FlankAngleThreshold = 120f;
        private const float SuppressionCrouchDuration = 2.5f;

        private float _suppressedUntil = 0f;

        public BotPoseController(BotOwner bot, BotComponentCache cache)
        {
            _bot = bot;
            _cache = cache;
            _movement = bot.GetPlayer.MovementContext;
            _personality = _cache.AIRefactoredBotOwner?.PersonalityProfile ?? new BotPersonalityProfile();
        }

        /// <summary>
        /// Called from BotBrain.Tick() to update posture logic.
        /// </summary>
        public void Tick(float time)
        {
            if (time < _nextPoseCheck)
                return;

            _nextPoseCheck = time + PoseCheckInterval;

            if (CheckSuppression(time)) return;
            if (CheckCoverProne()) return;
            if (CheckSmartProne()) return;
            if (CheckCoverCrouch()) return;

            HandleDefaultStance();
        }

        private bool CheckSuppression(float time)
        {
            if (_cache.Suppression?.IsSuppressed() == true)
            {
                _suppressedUntil = time + SuppressionCrouchDuration;
            }

            if (time < _suppressedUntil)
            {
                _movement.SetPoseLevel(50); // crouch
                return true;
            }

            return false;
        }

        private bool CheckSmartProne()
        {
            bool isSniper = _personality.Personality == PersonalityType.Sniper;
            bool isFearful = _personality.IsFearful || _personality.Personality == PersonalityType.Fearful;
            bool isPanicking = _cache.PanicHandler?.IsPanicking == true;

            if (!(isSniper || isFearful || isPanicking))
                return false;

            Vector3? flankDir = BotMemoryExtensions.TryGetFlankDirection(_bot);
            if (flankDir.HasValue)
            {
                float angle = Vector3.Angle(_bot.LookDirection, flankDir.Value.normalized);
                if (angle > FlankAngleThreshold)
                {
                    _movement.SetPoseLevel(0); // prone
                    return true;
                }
            }

            if (isPanicking)
            {
                _movement.SetPoseLevel(0); // prone
                return true;
            }

            return false;
        }

        private bool CheckCoverProne()
        {
            var cover = _bot.Memory?.BotCurrentCoverInfo?.LastCover;
            if (cover != null && BotCoverHelper.IsProneCover(cover))
            {
                _movement.SetPoseLevel(0); // prone
                return true;
            }

            return false;
        }

        private bool CheckCoverCrouch()
        {
            var cover = _bot.Memory?.BotCurrentCoverInfo?.LastCover;
            if (cover != null && BotCoverHelper.IsLowCover(cover))
            {
                _movement.SetPoseLevel(50); // crouch
                return true;
            }

            return false;
        }

        private void HandleDefaultStance()
        {
            bool inCombat = _cache.Combat?.IsInCombatState() == true;
            bool crouchPreferred = _personality.Caution > 0.6f || _personality.IsCamper;

            if (inCombat && crouchPreferred)
            {
                _movement.SetPoseLevel(50); // crouch
            }
            else
            {
                _movement.SetPoseLevel(100); // full stand
            }
        }
    }
}
