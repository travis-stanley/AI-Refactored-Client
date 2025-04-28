#nullable enable

namespace AIRefactored.AI.Perception
{
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Groups;
    using AIRefactored.AI.Memory;
    using AIRefactored.Core;

    using EFT;

    using UnityEngine;

    /// <summary>
    ///     Simulates realistic bot visual perception using view cone, distance, fog/light occlusion,
    ///     bone confidence, suppression stress, and tactical memory.
    /// </summary>
    public sealed class BotVisionSystem
    {
        private const float AutoDetectRadius = 4f;

        private const float BaseViewConeAngle = 120f;

        private const float BoneConfidenceDecay = 0.1f;

        private const float BoneConfidenceThreshold = 0.45f;

        private const float MaxDetectionDistance = 120f;

        private const float SuppressionMissChance = 0.2f;

        private static readonly PlayerBoneType[] BonesToCheck =
            {
                PlayerBoneType.Head, PlayerBoneType.Spine, PlayerBoneType.Ribcage, PlayerBoneType.LeftShoulder,
                PlayerBoneType.RightShoulder, PlayerBoneType.Pelvis, PlayerBoneType.LeftThigh1,
                PlayerBoneType.RightThigh1
            };

        private BotOwner? _bot;

        private BotComponentCache? _cache;

        private float _lastCommitTime = -999f;

        private BotTacticalMemory? _memory;

        private BotPersonalityProfile? _profile;

        public void Initialize(BotComponentCache cache)
        {
            this._cache = cache;
            this._bot = cache.Bot;
            this._profile = cache.AIRefactoredBotOwner?.PersonalityProfile;
            this._memory = cache.TacticalMemory;
        }

        public void Tick(float time)
        {
            if (!this.IsValidContext())
                return;

            var eye = this._bot!.Position + Vector3.up * 1.4f;
            var forward = this._bot.LookDirection;

            var fogPenalty = RenderSettings.fog ? Mathf.Clamp01(RenderSettings.fogDensity * 4f) : 0f;
            var ambientLight = RenderSettings.ambientLight.grayscale;
            var adjustedViewCone = Mathf.Lerp(BaseViewConeAngle, 60f, 1f - ambientLight);

            Player? bestTarget = null;
            var bestScore = float.MaxValue;

            foreach (var target in GameWorldHandler.GetAllAlivePlayers())
            {
                if (!this.IsValidTarget(target)) continue;

                var dist = Vector3.Distance(eye, target.Position);
                if (dist > MaxDetectionDistance * (1f - fogPenalty)) continue;

                var withinAutoRange = dist <= AutoDetectRadius;
                var inViewCone = IsInViewCone(forward, eye, target.Position, adjustedViewCone);
                var canSee = HasLineOfSight(eye, target);

                if ((withinAutoRange && canSee) || (inViewCone && canSee))
                {
                    var score = dist;
                    if (score < bestScore)
                    {
                        bestTarget = target;
                        bestScore = score;
                    }
                }
            }

            if (bestTarget != null)
            {
                this._memory!.RecordEnemyPosition(bestTarget.Position);
                this.TrackVisibleBones(this._bot.Position + Vector3.up * 1.4f, bestTarget);
                this.EvaluateTargetConfidence(bestTarget, time);
            }
        }

        private static bool HasLineOfSight(Vector3 from, Player target)
        {
            var to = target.Position + Vector3.up * 1.4f;
            if (Physics.Linecast(from, to, out var hit))
                return hit.collider?.transform.root == target.Transform?.Original?.root;

            return true;
        }

        private static bool IsInViewCone(Vector3 forward, Vector3 origin, Vector3 targetPos, float viewCone)
        {
            var angle = Vector3.Angle(forward, targetPos - origin);
            return angle <= viewCone * 0.5f;
        }

        private void CommitEnemyIfAllowed(Player target, float time)
        {
            if (this._bot == null || this._profile == null || this._bot.EnemiesController == null)
                return;

            if (this._bot.EnemiesController.EnemyInfos.ContainsKey(target))
                return;

            var delay = Mathf.Lerp(0.1f, 0.6f, 1f - this._profile.ReactionTime);
            if (time - this._lastCommitTime < delay)
                return;

            var group = this._bot.BotsGroup;
            var settings = new BotSettingsClass(target, group, EBotEnemyCause.addPlayer);

            this._bot.EnemiesController.AddNew(group, target, settings);
            this._bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyConversation);
            BotTeamLogic.AddEnemy(this._bot, target);

            this._lastCommitTime = time;
        }

        private void EvaluateTargetConfidence(Player target, float time)
        {
            var tracker = this._cache!.VisibilityTracker;
            if (tracker == null || !tracker.HasEnoughData)
                return;

            var confidence = tracker.GetOverallConfidence();
            if (confidence < BoneConfidenceThreshold)
                return;

            if (this._cache.PanicHandler?.IsUnderSuppression == true && Random.value < SuppressionMissChance)
                return;

            this.CommitEnemyIfAllowed(target, time);
        }

        private bool IsValidContext()
        {
            return this._bot?.IsDead == false && this._cache != null && this._profile != null && this._memory != null
                   && this._bot.GetPlayer is { IsAI: true, IsYourPlayer: false };
        }

        private bool IsValidTarget(Player target)
        {
            if (target.HealthController?.IsAlive != true)
                return false;

            if (target.ProfileId == this._bot!.ProfileId || target == this._bot.GetPlayer)
                return false;

            return this._bot.BotsGroup?.IsEnemy(target) == true;
        }

        private void TrackVisibleBones(Vector3 eye, Player target)
        {
            var tracker = this._cache!.VisibilityTracker ??= new TrackedEnemyVisibility(this._bot!.Transform.Original);

            if (target.TryGetComponent<PlayerSpiritBones>(out var bones))
                foreach (var type in BonesToCheck)
                {
                    var bone = bones.GetBone(type)?.Original;
                    if (bone == null)
                        continue;

                    if (!Physics.Linecast(eye, bone.position, out var hit)
                        || hit.collider?.gameObject == target.gameObject)
                        tracker.UpdateBoneVisibility(type.ToString(), bone.position);
                }
            else if (Physics.Linecast(eye, target.Position, out var fallbackHit)
                     && fallbackHit.collider?.transform.root == target.Transform?.Original?.root)
                tracker.UpdateBoneVisibility("Body", target.Position);

            tracker.DecayConfidence(BoneConfidenceDecay * Time.deltaTime);
        }
    }
}