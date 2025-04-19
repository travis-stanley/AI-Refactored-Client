#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Group;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Perception;
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
        private const float ProximityAutoDetectDistance = 4.0f;

        private static readonly PlayerBoneType[] BonesToCheck =
        {
            PlayerBoneType.Head,
            PlayerBoneType.Spine,
            PlayerBoneType.Ribcage,
            PlayerBoneType.LeftShoulder,
            PlayerBoneType.RightShoulder,
            PlayerBoneType.Pelvis,
            PlayerBoneType.LeftThigh1,
            PlayerBoneType.RightThigh1
        };

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private AIRefactoredBotOwner? _owner;
        private BotPersonalityProfile? _profile;
        private BotTacticalMemory? _memory;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _owner = GetComponent<AIRefactoredBotOwner>();
            _profile = _owner?.PersonalityProfile;
            _memory = GetComponent<BotTacticalMemory>();
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

                // === Instant auto-detect at close range ===
                if (distance <= ProximityAutoDetectDistance)
                {
                    ForceEnemyCommit(target);
                    TrackVisibleBones(target);
                    continue;
                }

                if (EvaluateTarget(target))
                {
                    TrackVisibleBones(target);
                }
            }
        }

        #endregion

        #region Visibility Logic

        private bool EvaluateTarget(Player target)
        {
            if (_bot == null)
                return false;

            Vector3 toTarget = target.Position - _bot.Position;
            float angle = Vector3.Angle(_bot.LookDirection, toTarget);
            if (angle > FieldOfViewDegrees * 0.5f)
                return false;

            Vector3 eyePosBot = _bot.Position + Vector3.up * 1.4f;
            Vector3 eyePosTarget = target.Position + Vector3.up * 1.4f;

            if (Physics.Linecast(eyePosBot, eyePosTarget, out var hit) &&
                hit.collider != null &&
                hit.collider.gameObject != target.gameObject)
            {
                return false;
            }

            _memory?.RecordEnemyPosition(target.Position);

            if (_profile.ReactionSpeed >= 0.5f)
            {
                ForceEnemyCommit(target);
            }

            return true;
        }

        private void ForceEnemyCommit(Player target)
        {
            if (_bot?.Memory == null || target == null)
                return;

            if (!_bot.EnemiesController.EnemyInfos.ContainsKey(target))
            {
                var enemySettings = new BotSettingsClass(target, _bot.BotsGroup, EBotEnemyCause.addPlayer);
                _bot.EnemiesController.AddNew(_bot.BotsGroup, target, enemySettings);
            }

            _bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyConversation);
            BotTeamLogic.AddEnemy(_bot, target);
        }

        private void TrackVisibleBones(Player target)
        {
            if (_cache == null || _bot == null)
                return;

            var spirit = target.GetComponent<PlayerSpiritBones>();
            if (spirit == null)
                return;

            Vector3 origin = _bot.Position + Vector3.up * 1.4f;

            foreach (var boneType in BonesToCheck)
            {
                var bone = spirit.GetBone(boneType);
                if (bone?.Original == null)
                    continue;

                Vector3 targetPoint = bone.Original.position;

                if (!Physics.Linecast(origin, targetPoint, out var hit) || hit.collider?.gameObject == target.gameObject)
                {
                    _cache.VisibilityTracker ??= new TrackedEnemyVisibility(transform);
                    _cache.VisibilityTracker.UpdateBoneVisibility(boneType.ToString(), targetPoint);
                }
            }
        }

        #endregion
    }
}
