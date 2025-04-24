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
    /// Simulates realistic bot visual perception using view cone, distance, fog/light occlusion,
    /// bone confidence, suppression stress, and tactical memory.
    /// </summary>
    public sealed class BotVisionSystem
    {
        #region Constants

        private const float MaxDetectionDistance = 120f;
        private const float BaseViewConeAngle = 120f;
        private const float AutoDetectRadius = 4f;
        private const float BoneConfidenceThreshold = 0.45f;
        private const float BoneConfidenceDecay = 0.1f;
        private const float SuppressionMissChance = 0.2f;

        private static readonly PlayerBoneType[] BonesToCheck =
        {
            PlayerBoneType.Head, PlayerBoneType.Spine, PlayerBoneType.Ribcage,
            PlayerBoneType.LeftShoulder, PlayerBoneType.RightShoulder,
            PlayerBoneType.Pelvis, PlayerBoneType.LeftThigh1, PlayerBoneType.RightThigh1
        };

        #endregion

        #region Fields

        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotPersonalityProfile? _profile;
        private BotTacticalMemory? _memory;
        private float _lastCommitTime = -999f;

        #endregion

        #region Initialization

        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
            _profile = cache.AIRefactoredBotOwner?.PersonalityProfile;
            _memory = cache.TacticalMemory;
        }

        #endregion

        #region Main Tick

        public void Tick(float time)
        {
            if (!IsValidContext())
                return;

            Vector3 eye = _bot!.Position + Vector3.up * 1.4f;
            Vector3 forward = _bot.LookDirection;

            float fogPenalty = RenderSettings.fog ? Mathf.Clamp01(RenderSettings.fogDensity * 4f) : 0f;
            float ambientLight = RenderSettings.ambientLight.grayscale;
            float adjustedViewCone = Mathf.Lerp(BaseViewConeAngle, 60f, 1f - ambientLight);

            Player? bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (var target in GameWorldHandler.GetAllAlivePlayers())
            {
                if (!IsValidTarget(target)) continue;

                float dist = Vector3.Distance(eye, target.Position);
                if (dist > MaxDetectionDistance * (1f - fogPenalty)) continue;

                bool withinAutoRange = dist <= AutoDetectRadius;
                bool inViewCone = IsInViewCone(forward, eye, target.Position, adjustedViewCone);
                bool canSee = HasLineOfSight(eye, target);

                if ((withinAutoRange && canSee) || (inViewCone && canSee))
                {
                    float score = dist;
                    if (score < bestScore)
                    {
                        bestTarget = target;
                        bestScore = score;
                    }
                }
            }

            if (bestTarget != null)
            {
                _memory!.RecordEnemyPosition(bestTarget.Position);
                TrackVisibleBones(_bot.Position + Vector3.up * 1.4f, bestTarget);
                EvaluateTargetConfidence(bestTarget, time);
            }
        }

        #endregion

        #region Evaluation

        private void EvaluateTargetConfidence(Player target, float time)
        {
            var tracker = _cache!.VisibilityTracker;
            if (tracker == null || !tracker.HasEnoughData)
                return;

            float confidence = tracker.GetOverallConfidence();
            if (confidence < BoneConfidenceThreshold)
                return;

            if (_cache.PanicHandler?.IsUnderSuppression == true && Random.value < SuppressionMissChance)
                return;

            CommitEnemyIfAllowed(target, time);
        }

        private bool IsValidContext()
        {
            return _bot?.IsDead == false &&
                   _cache != null &&
                   _profile != null &&
                   _memory != null &&
                   _bot.GetPlayer is { IsAI: true, IsYourPlayer: false };
        }

        private bool IsValidTarget(Player target)
        {
            if (target.HealthController?.IsAlive != true)
                return false;

            if (target.ProfileId == _bot!.ProfileId || target == _bot.GetPlayer)
                return false;

            return _bot.BotsGroup?.IsEnemy(target) == true;
        }

        private static bool IsInViewCone(Vector3 forward, Vector3 origin, Vector3 targetPos, float viewCone)
        {
            float angle = Vector3.Angle(forward, targetPos - origin);
            return angle <= viewCone * 0.5f;
        }

        private static bool HasLineOfSight(Vector3 from, Player target)
        {
            Vector3 to = target.Position + Vector3.up * 1.4f;
            if (Physics.Linecast(from, to, out RaycastHit hit))
                return hit.collider?.transform.root == target.Transform?.Original?.root;

            return true;
        }

        #endregion

        #region Memory Commit

        private void CommitEnemyIfAllowed(Player target, float time)
        {
            if (_bot == null || _profile == null || _bot.EnemiesController == null)
                return;

            if (_bot.EnemiesController.EnemyInfos.ContainsKey(target))
                return;

            float delay = Mathf.Lerp(0.1f, 0.6f, 1f - _profile.ReactionTime);
            if (time - _lastCommitTime < delay)
                return;

            var group = _bot.BotsGroup;
            var settings = new BotSettingsClass(target, group, EBotEnemyCause.addPlayer);

            _bot.EnemiesController.AddNew(group, target, settings);
            _bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyConversation);
            BotTeamLogic.AddEnemy(_bot, target);

            _lastCommitTime = time;
        }

        #endregion

        #region Bone Visibility

        private void TrackVisibleBones(Vector3 eye, Player target)
        {
            var tracker = _cache!.VisibilityTracker ??= new TrackedEnemyVisibility(_bot!.Transform.Original);

            if (target.TryGetComponent<PlayerSpiritBones>(out var bones))
            {
                foreach (var type in BonesToCheck)
                {
                    var bone = bones.GetBone(type)?.Original;
                    if (bone == null)
                        continue;

                    if (!Physics.Linecast(eye, bone.position, out RaycastHit hit) || hit.collider?.gameObject == target.gameObject)
                        tracker.UpdateBoneVisibility(type.ToString(), bone.position);
                }
            }
            else if (Physics.Linecast(eye, target.Position, out RaycastHit fallbackHit) &&
                     fallbackHit.collider?.transform.root == target.Transform?.Original?.root)
            {
                tracker.UpdateBoneVisibility("Body", target.Position);
            }

            tracker.DecayConfidence(BoneConfidenceDecay * Time.deltaTime);
        }

        #endregion
    }
}
