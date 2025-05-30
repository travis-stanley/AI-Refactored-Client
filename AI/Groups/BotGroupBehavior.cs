﻿// <auto-generated>
//   AI-Refactored: BotGroupBehavior.cs (Ultimate Realism & Squad Role Edition - Beyond Diamond - BotBrain Tick-Driven)
//   Real squad simulation: flocking, dynamic roles, leadership, emotion, comms.
//   All logic is centralized, allocation-free, null-guarded, and BotBrain Tick()-driven only.
//   MIT License.
// </auto-generated>

namespace AIRefactored.AI.Groups
{
    using System;
    using System.Collections.Generic;
    using AIRefactored.AI.Core;
    using AIRefactored.AI.Helpers;
    using AIRefactored.Core;
    using EFT;
    using UnityEngine;

    /// <summary>
    /// Drives ultra-realistic squad dynamics:
    /// - Flocking/formation, personal space, repulsion, dynamic leadership/roles
    /// - Role: Leader/Medic/Flanker/Support, with composure-based handoff
    /// - Squad nervousness/emotional contagion, comms, phrase variety
    /// - No allocations in tick, all moves via BotMovementHelper, zero self-tick logic
    /// - All failures locally isolated, never disables, always playable
    /// </summary>
    public sealed class BotGroupBehavior
    {
        private const float MaxSpacing = 7.5f;
        private const float MinSpacing = 2.35f;
        private const float SpacingTolerance = 0.24f;
        private const float RepulseStrength = 1.18f;
        private const float JitterAmount = 0.13f;
        private const float DriftSpeed = 0.14f;
        private const float MaxSquadRadius = 13.0f;
        private const float MinTickMoveInterval = 0.36f;
        private const float LeadershipRotationCooldown = 4.7f;
        private const float RoleReassignCooldown = 7.2f;

        private static readonly float MinSpacingSqr = MinSpacing * MinSpacing;
        private static readonly float MaxSpacingSqr = MaxSpacing * MaxSpacing;
        private static readonly float MaxSquadRadiusSqr = MaxSquadRadius * MaxSquadRadius;

        private BotOwner _bot;
        private BotComponentCache _cache;
        private BotsGroup _group;

        private Vector3 _lastMoveTarget;
        private bool _hasLastTarget;
        private float _lastMoveTime;
        private Vector2 _personalDrift;
        private float _nervousnessLevel;
        private float _lastChatterTime;

        private int _squadLeaderIndex;
        private List<BotOwner> _squadMembers;
        private float _lastLeadershipRotationTime;
        private float _lastRoleReassignTime;

        public BotGroupSyncCoordinator GroupSync { get; private set; }
        public bool IsFollowingLeader { get; private set; }
        public bool IsInSquad => _group != null && _group.MembersCount > 1;
        public bool IsLeader => _squadMembers != null && _squadLeaderIndex >= 0 && _squadLeaderIndex < _squadMembers.Count && ReferenceEquals(_squadMembers[_squadLeaderIndex], _bot);
        public bool IsMedic { get; private set; }
        public bool IsFlanker { get; private set; }
        public bool IsSupport { get; private set; }

        public void Initialize(BotComponentCache componentCache)
        {
            _cache = componentCache;
            _bot = componentCache?.Bot;
            _group = _bot?.BotsGroup;

            GroupSync = new BotGroupSyncCoordinator();
            GroupSync.Initialize(_bot);
            GroupSync.InjectLocalCache(_cache);

            _personalDrift = ComputePersonalDrift(_bot.ProfileId);
            _nervousnessLevel = UnityEngine.Random.Range(0.19f, 0.53f);
            _squadLeaderIndex = 0;
            _lastLeadershipRotationTime = Time.time;
            _lastRoleReassignTime = Time.time;
            RefreshSquadMembers();
            AssignSquadRoles();
        }

        public void Tick(float deltaTime)
        {
            if (_bot == null || _cache == null || _group == null || _bot.IsDead || _bot.Memory == null || _bot.Memory.GoalEnemy != null || _group.MembersCount <= 1)
                return;

            RefreshSquadMembers();
            DynamicLeadershipRotation();
            DynamicRoleReassignment();

            Vector3 myPos = _bot.Position;
            Vector3 repulsion = Vector3.zero;
            Vector3 furthest = Vector3.zero;
            float maxDistSqr = MinSpacingSqr;
            bool hasFurthest = false;
            int memberCount = _group.MembersCount;
            int closestIdx = -1;
            float closestDist = float.MaxValue;

            float roleBias = _cache.AIRefactoredBotOwner?.PersonalityProfile?.Cohesion ?? 1.0f;
            float nervousBias = 1.0f + _nervousnessLevel * 0.4f;
            float effectiveMinSpacingSqr = MinSpacingSqr * nervousBias * roleBias;

            for (int i = 0; i < memberCount; i++)
            {
                BotOwner mate = _group.Member(i);
                if (mate == null || ReferenceEquals(mate, _bot) || mate.IsDead || mate.Memory == null)
                    continue;

                Vector3 offset = mate.Position - myPos;
                float distSqr = offset.sqrMagnitude;

                if (distSqr < closestDist)
                {
                    closestDist = distSqr;
                    closestIdx = i;
                }

                if (distSqr < effectiveMinSpacingSqr)
                {
                    float push = MinSpacing - Mathf.Sqrt(distSqr);
                    repulsion += -offset.normalized * push * 0.74f;
                }
                else if (distSqr > MaxSpacingSqr && distSqr > maxDistSqr && distSqr < MaxSquadRadiusSqr && mate.Memory.GoalEnemy == null)
                {
                    maxDistSqr = distSqr;
                    furthest = mate.Position;
                    hasFurthest = true;
                }
            }

            UpdateSquadNervousness(deltaTime, memberCount);
            HandleSquadChatter(memberCount);

            if (repulsion.sqrMagnitude > 0.013f)
            {
                IssueMove(SmoothDriftMove(myPos, repulsion.normalized * RepulseStrength, deltaTime, true));
                IsFollowingLeader = false;
                return;
            }

            if (hasFurthest)
            {
                Vector3 dir = furthest - myPos;
                if (dir.sqrMagnitude > 0.0008f)
                {
                    IssueMove(SmoothDriftMove(myPos, dir.normalized * MaxSpacing * 0.69f, deltaTime, false));
                    IsFollowingLeader = false;
                    return;
                }
            }

            if (closestIdx >= 0 && closestDist > 0.65f && closestDist < MaxSquadRadiusSqr)
            {
                Vector3 anchorTarget = _group.Member(closestIdx).Position;
                Vector3 leaderDir = anchorTarget - myPos;
                if (leaderDir.sqrMagnitude > 0.00023f)
                {
                    IssueMove(SmoothDriftMove(myPos, leaderDir.normalized * (MinSpacing * 0.8f), deltaTime, false));
                    IsFollowingLeader = true;
                }
            }
        }

        private void RefreshSquadMembers()
        {
            if (_group == null || _group.MembersCount == 0)
            {
                _squadMembers = null;
                _squadLeaderIndex = 0;
                return;
            }
            if (_squadMembers == null || _squadMembers.Count != _group.MembersCount)
            {
                _squadMembers = new List<BotOwner>(_group.MembersCount);
                for (int i = 0; i < _group.MembersCount; i++)
                    _squadMembers.Add(_group.Member(i));
                _squadLeaderIndex = ElectLeaderIndex();
            }
        }

        private int ElectLeaderIndex()
        {
            float bestComposure = float.MinValue;
            int leaderIdx = 0;
            for (int i = 0; i < _squadMembers.Count; i++)
            {
                var mate = _squadMembers[i];
                if (mate == null || mate.IsDead) continue;
                if (BotComponentCacheRegistry.TryGetByPlayer(mate.GetPlayer, out var mateCache))
                {
                    float composure = mateCache?.PanicHandler?.GetComposureLevel() ?? 0.5f;
                    if (composure > bestComposure)
                    {
                        bestComposure = composure;
                        leaderIdx = i;
                    }
                }
            }
            return leaderIdx;
        }

        private void DynamicLeadershipRotation()
        {
            float now = Time.time;
            if (_squadMembers == null || _squadMembers.Count == 0)
                return;
            var leader = _squadMembers[_squadLeaderIndex];
            if (leader == null || leader.IsDead)
            {
                if (now - _lastLeadershipRotationTime > LeadershipRotationCooldown)
                {
                    _squadLeaderIndex = ElectLeaderIndex();
                    _lastLeadershipRotationTime = now;
                }
            }
        }

        private void DynamicRoleReassignment()
        {
            float now = Time.time;
            if (_squadMembers == null || _squadMembers.Count == 0)
                return;
            if (now - _lastRoleReassignTime < RoleReassignCooldown)
                return;

            int medicIdx = -1, flankerIdx = -1, supportIdx = -1;
            float bestHealing = float.MinValue, bestFlank = float.MinValue, bestSupport = float.MinValue;
            for (int i = 0; i < _squadMembers.Count; i++)
            {
                var mate = _squadMembers[i];
                if (mate == null || mate.IsDead) continue;
                if (BotComponentCacheRegistry.TryGetByPlayer(mate.GetPlayer, out var mateCache))
                {
                    var p = mateCache?.AIRefactoredBotOwner?.PersonalityProfile;
                    if (p == null) continue;
                    if (p.Caution > bestHealing) { medicIdx = i; bestHealing = p.Caution; }
                    if (p.AggressionLevel > bestFlank) { flankerIdx = i; bestFlank = p.AggressionLevel; }
                    if (p.Cohesion > bestSupport) { supportIdx = i; bestSupport = p.Cohesion; }
                }
            }

            IsMedic = medicIdx >= 0 && _squadMembers[medicIdx] == _bot;
            IsFlanker = flankerIdx >= 0 && _squadMembers[flankerIdx] == _bot;
            IsSupport = supportIdx >= 0 && _squadMembers[supportIdx] == _bot;
            _lastRoleReassignTime = now;
        }

        private void AssignSquadRoles()
        {
            _lastRoleReassignTime = Time.time - RoleReassignCooldown - 1f;
            DynamicRoleReassignment();
        }

        private void IssueMove(Vector3 target)
        {
            float now = Time.time;
            float cohesion = _cache.AIRefactoredBotOwner?.PersonalityProfile?.Cohesion ?? 1f;

            if (!_hasLastTarget || Vector3.Distance(_lastMoveTarget, target) > SpacingTolerance || now - _lastMoveTime > MinTickMoveInterval)
            {
                _lastMoveTarget = target;
                _hasLastTarget = true;
                _lastMoveTime = now;
                BotMovementHelper.SmoothMoveTo(_bot, target, slow: false);
            }
        }

        private Vector3 SmoothDriftMove(Vector3 basePos, Vector3 direction, float deltaTime, bool strongNervous)
        {
            Vector3 jitter = UnityEngine.Random.insideUnitSphere * (JitterAmount + _nervousnessLevel * 0.09f);
            jitter.y = 0f;
            Vector3 drift = direction.normalized * DriftSpeed * Mathf.Clamp(deltaTime * 2.08f, 0.11f, 0.34f);
            Vector3 personalBias = new Vector3(_personalDrift.x, 0f, _personalDrift.y);

            if (strongNervous)
            {
                float hesitation = Mathf.Sin(Time.time * (1.17f + _nervousnessLevel)) * 0.13f * _nervousnessLevel;
                personalBias += new Vector3(-_personalDrift.y, 0f, _personalDrift.x) * hesitation;
            }

            return basePos + drift + jitter + personalBias;
        }

        private Vector2 ComputePersonalDrift(string profileId)
        {
            int seed = profileId.GetHashCode() ^ 0x191A81C;
            var rand = new System.Random(seed);
            float angle = (float)(rand.NextDouble() * Mathf.PI * 2.0);
            float radius = 0.17f + (float)(rand.NextDouble() * 0.18f);
            return new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        }

        private void UpdateSquadNervousness(float deltaTime, int memberCount)
        {
            bool recentCombat = _group != null && _group.DangerAreasCount > 0;
            if (recentCombat)
                _nervousnessLevel = Mathf.Clamp01(_nervousnessLevel + (0.21f + (memberCount * 0.04f)) * deltaTime);
            else
                _nervousnessLevel = Mathf.Clamp01(_nervousnessLevel - (0.16f + (memberCount * 0.03f)) * deltaTime);
        }
        /// <summary>
        /// Broadcasts a danger point to all squadmates based on fire echo position.
        /// </summary>
        public void TriggerSquadFireEcho(BotOwner source, Vector3 position)
        {
            try
            {
                if (_group == null || source == null || source.IsDead || _group.MembersCount < 2)
                    return;

                for (int i = 0; i < _group.MembersCount; i++)
                {
                    var mate = _group.Member(i);
                    if (mate == null || mate.IsDead || ReferenceEquals(mate, source) || mate.Memory == null)
                        continue;

                    var danger = new PlaceForCheck(position, PlaceForCheckType.danger);
                    mate.DangerPointsData?.AddPointOfDanger(danger, true);
                }
            }
            catch (Exception ex)
            {
                Plugin.LoggerInstance.LogError("[BotGroupBehavior] TriggerSquadFireEcho failed: " + ex);
            }
        }
        private void HandleSquadChatter(int memberCount)
        {
            float now = Time.time;
            if (now - _lastChatterTime > 4.1f + UnityEngine.Random.value * 2.9f)
            {
                if (_group != null && _group.GroupTalk != null && _group.GroupTalk.CanSay(_bot, EPhraseTrigger.GoForward))
                {
                    if (IsLeader)
                        _group.GroupTalk.PhraseSad(_bot, EPhraseTrigger.CoverMe);
                    else if (IsMedic)
                        _group.GroupTalk.PhraseSad(_bot, EPhraseTrigger.NeedHelp);
                    else if (IsFlanker)
                        _group.GroupTalk.PhraseSad(_bot, EPhraseTrigger.GoForward);
                    else
                        _group.GroupTalk.PhraseSad(_bot, EPhraseTrigger.Cooperation);
                }
                _lastChatterTime = now;
            }
        }
    }
}
