#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Memory;
using AIRefactored.Core;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Perception
{
    /// <summary>
    /// Simulates realistic bot vision perception with real-time FOV and line-of-sight checks.
    /// Detects enemies every frame for smooth visual reactions.
    /// </summary>
    public class BotVisionSystem
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

        #region Initialization

        /// <summary>
        /// Sets up vision system with required bot context.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
            _owner = cache.AIRefactoredBotOwner;
            _profile = _owner?.PersonalityProfile;
            _memory = cache.TacticalMemory;
        }

        #endregion

        #region Tick Loop

        /// <summary>
        /// Called every frame by BotBrain for real-time vision processing.
        /// </summary>
        public void Tick(float time)
        {
            if (_bot == null || _bot.IsDead || _cache == null || _profile == null)
                return;

            Player? botPlayer = _bot.GetPlayer;
            if (botPlayer == null || botPlayer.IsYourPlayer)
                return;

            var allPlayers = GameWorldHandler.GetAllAlivePlayers();
            if (allPlayers == null)
                return;

            for (int i = 0; i < allPlayers.Count; i++)
            {
                Player? target = allPlayers[i];
                if (target == null || target.ProfileId == _bot.ProfileId || !target.HealthController.IsAlive)
                    continue;

                if (!target.IsAI && target.IsYourPlayer)
                    continue;

                float distance = Vector3.Distance(_bot.Position, target.Position);
                if (distance > MaxPlayerScanDistance)
                    continue;

                if (distance <= ProximityAutoDetectDistance)
                {
                    ForceEnemyCommit(target);
                    TrackVisibleBones(target);
                    continue;
                }

                if (IsInViewCone(target) && HasLineOfSight(target))
                {
                    _memory?.RecordEnemyPosition(target.Position);

                    if (_profile.ReactionSpeed >= 0.5f)
                        ForceEnemyCommit(target);

                    TrackVisibleBones(target);
                }
            }
        }

        #endregion

        #region Visibility Logic

        private bool IsInViewCone(Player target)
        {
            if (_bot == null)
                return false;

            Vector3 toTarget = target.Position - _bot.Position;
            float angle = Vector3.Angle(_bot.LookDirection, toTarget);
            return angle <= FieldOfViewDegrees * 0.5f;
        }

        private bool HasLineOfSight(Player target)
        {
            if (_bot == null)
                return false;

            Vector3 botEye = _bot.Position + Vector3.up * 1.4f;
            Vector3 targetEye = target.Position + Vector3.up * 1.4f;

            return !Physics.Linecast(botEye, targetEye, out var hit) ||
                   (hit.collider != null && hit.collider.gameObject == target.gameObject);
        }

        private void ForceEnemyCommit(Player target)
        {
            if (_bot == null || _bot.Memory == null || target == null)
                return;

            if (!_bot.EnemiesController.EnemyInfos.ContainsKey(target))
            {
                var group = _bot.BotsGroup;
                var enemySettings = new BotSettingsClass(target, group, EBotEnemyCause.addPlayer);
                _bot.EnemiesController.AddNew(group, target, enemySettings);
                _bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyConversation);
                BotTeamLogic.AddEnemy(_bot, target);
            }
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

                Vector3 point = bone.Original.position;
                bool canSee = !Physics.Linecast(origin, point, out RaycastHit hit) ||
                              hit.collider?.gameObject == target.gameObject;

                if (canSee)
                {
                    if (_cache.VisibilityTracker == null && _bot.Transform?.Original != null)
                    {
                        _cache.VisibilityTracker = new TrackedEnemyVisibility(_bot.Transform.Original);
                    }

                    _cache.VisibilityTracker?.UpdateBoneVisibility(boneType.ToString(), point);
                }
            }
        }

        #endregion
    }
}
