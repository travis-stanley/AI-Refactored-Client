#nullable enable

using AIRefactored.AI.Core;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Simulates realistic bot vision perception with real-time FOV and line-of-sight checks.
    /// Detects enemies every frame for smooth visual reactions.
    /// </summary>
    public class BotVisionSystem : MonoBehaviour
    {
        #region Constants

        private const float MaxPlayerScanDistance = 120f;
        private const float FieldOfViewDegrees = 120f;

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private AIRefactoredBotOwner? _owner;
        private BotPersonalityProfile? _profile;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _owner = GetComponent<AIRefactoredBotOwner>();
            _profile = _owner?.PersonalityProfile;
        }

        #endregion

        #region Public Tick

        /// <summary>
        /// Called every frame by BotBrain for real-time vision processing.
        /// </summary>
        public void Tick(float time)
        {
            if (_bot == null || _bot.IsDead || _cache == null || _profile == null)
                return;

            if (_bot.GetPlayer == null || _bot.GetPlayer.IsYourPlayer)
                return;

            var allPlayers = Singleton<GameWorld>.Instance?.RegisteredPlayers;
            if (allPlayers == null)
                return;

            for (int i = 0; i < allPlayers.Count; i++)
            {
                var other = allPlayers[i];
                if (other == null || other.ProfileId == _bot.ProfileId || !other.HealthController.IsAlive)
                    continue;

                if (!other.IsAI && other.IsYourPlayer)
                    continue;

                if (other is not Player target)
                    continue;

                float distance = Vector3.Distance(_bot.Position, target.Position);
                if (distance > MaxPlayerScanDistance)
                    continue;

                EvaluateTarget(target);
            }
        }

        #endregion

        #region Internal Logic

        private void EvaluateTarget(Player target)
        {
            if (_bot == null)
                return;

            Vector3 toTarget = target.Position - _bot.Position;
            float angle = Vector3.Angle(_bot.LookDirection, toTarget);

            if (angle > FieldOfViewDegrees * 0.5f)
                return;

            Vector3 eyePosBot = _bot.Position + Vector3.up * 1.4f;
            Vector3 eyePosTarget = target.Position + Vector3.up * 1.4f;

            if (Physics.Linecast(eyePosBot, eyePosTarget, out var hit))
            {
                if (hit.collider != null && hit.collider.gameObject != target.gameObject)
                    return;
            }

            _bot.Memory?.AddEnemy(target, null, onActivation: false);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyConversation);
        }

        #endregion
    }
}
