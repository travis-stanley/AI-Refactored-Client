#nullable enable

using EFT;
using UnityEngine;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Handles real-time movement logic such as corner clearing, stagger adjustment, and fallback peeking.
    /// Executed continuously via BotBrain.
    /// </summary>
    public class BotMovementController : MonoBehaviour
    {
        private BotOwner? _bot;

        private const float CornerScanInterval = 1.2f;
        private float _nextScanTime = 0f;

        private const float ScanDistance = 2.5f;
        private const float ScanRadius = 0.25f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
        }

        /// <summary>
        /// Ticked per frame by BotBrain. Handles smooth peeking and forward scanning behavior.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_bot == null || !_bot.IsAI || _bot.IsDead || _bot.GetPlayer == null || _bot.GetPlayer.IsYourPlayer)
                return;

            float now = Time.time;

            // === Optional forward scan check
            if (now >= _nextScanTime)
            {
                ScanAhead();
                _nextScanTime = now + CornerScanInterval;
            }

            // === Continuously look where you're going
            if (_bot.Mover != null)
            {
                Vector3 lookTarget = _bot.Mover.LastTargetPoint(1.0f); // Use default distCoef
                BotMovementHelper.SmoothLookTo(_bot, lookTarget, speed: 4f);
            }
        }

        private void ScanAhead()
        {
            if (_bot == null || _bot.Mover == null)
                return;

            Vector3 origin = _bot.Position + Vector3.up * 1.5f;
            Vector3 forward = _bot.LookDirection;

            if (Physics.SphereCast(origin, ScanRadius, forward, out RaycastHit hit, ScanDistance))
            {
                if (hit.collider != null)
                {
                    // Pause or peek logic can go here if desired
                    _bot.BotTalk?.TrySay(EPhraseTrigger.Look);
                }
            }
        }
    }
}
