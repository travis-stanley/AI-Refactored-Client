#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Missions;
using AIRefactored.Core;
using Comfort.Common;
using EFT;
using EFT.Bots;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using MissionType = AIRefactored.AI.Missions.BotMissionController.MissionType;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Handles squad-level tactical coordination:
    /// Enemy sharing, fallback broadcast, regrouping, and mission voice triggers.
    /// </summary>
    public sealed class BotTeamLogic
    {
        #region Fields

        private readonly BotOwner _bot;
        private readonly List<BotOwner> _teammates = new(8);
        private readonly Dictionary<BotOwner, CombatStateMachine> _combatMap = new(8);

        private const float RegroupJitterRadius = 1.5f;
        private const float RegroupThreshold = 2.5f;

        #endregion

        #region Constructor

        public BotTeamLogic(BotOwner bot)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        }

        #endregion

        #region Teammate Management

        public void SetTeammates(List<BotOwner> allBots)
        {
            _teammates.Clear();

            string? groupId = _bot.GetPlayer?.Profile?.Info?.GroupId;
            if (string.IsNullOrEmpty(groupId))
                return;

            foreach (var other in allBots)
            {
                if (other == null || other == _bot || other.IsDead)
                    continue;

                var player = other.GetPlayer;
                if (player?.AIData != null && player.Profile?.Info?.GroupId == groupId)
                    _teammates.Add(other);
            }
        }

        public void InjectCombatState(BotOwner mate, CombatStateMachine fsm)
        {
            if (mate == null || fsm == null || mate == _bot || _combatMap.ContainsKey(mate))
                return;

            _combatMap[mate] = fsm;
        }

        #endregion

        #region Enemy Sharing

        public void ShareTarget(IPlayer enemy)
        {
            if (enemy == null || string.IsNullOrWhiteSpace(enemy.ProfileId))
                return;

            var resolved = GameWorldHandler.Get()?.GetAlivePlayerByProfileID(enemy.ProfileId);
            if (resolved == null)
                return;

            foreach (var mate in _teammates)
                ForceRegisterEnemy(mate, resolved);
        }

        public static void AddEnemy(BotOwner bot, IPlayer target)
        {
            if (bot == null || bot.IsDead || target == null || bot.BotsGroup == null || bot.Memory == null)
                return;

            var resolved = GameWorldHandler.Get()?.GetAlivePlayerByProfileID(target.ProfileId ?? "");
            if (resolved == null)
                return;

            for (int i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                var mate = bot.BotsGroup.Member(i);
                if (mate == null || mate == bot || mate.IsDead || mate.BotsGroup == null || mate.Memory == null)
                    continue;

                ForceRegisterEnemy(mate, resolved);
            }
        }

        private static void ForceRegisterEnemy(BotOwner receiver, IPlayer enemy)
        {
            if (receiver == null || receiver.IsDead || enemy == null)
                return;

            if (!receiver.BotsGroup.IsEnemy(enemy))
                receiver.BotsGroup.AddEnemy(enemy, EBotEnemyCause.zryachiyLogic);

            if (!receiver.EnemiesController.EnemyInfos.ContainsKey(enemy))
            {
                var fallbackSettings = new BotSettingsClass(enemy as Player, receiver.BotsGroup, EBotEnemyCause.zryachiyLogic);
                receiver.Memory.AddEnemy(enemy, fallbackSettings, false);
            }
        }

        #endregion

        #region Movement Coordination

        public void CoordinateMovement()
        {
            if (_bot.IsDead || _teammates.Count == 0)
                return;

            Vector3 center = Vector3.zero;
            int count = 0;

            foreach (var mate in _teammates)
            {
                if (mate == null || mate.IsDead)
                    continue;

                center += mate.Position;
                count++;
            }

            if (count == 0)
                return;

            center /= count;
            Vector3 jitter = UnityEngine.Random.insideUnitSphere * RegroupJitterRadius;
            jitter.y = 0f;

            Vector3 target = center + jitter;
            float distSq = (_bot.Position - target).sqrMagnitude;

            if (distSq > RegroupThreshold * RegroupThreshold)
                BotMovementHelper.SmoothMoveTo(_bot, target, false, 1f);
        }

        #endregion

        #region Fallback Coordination

        public void BroadcastFallback(Vector3 retreatPoint)
        {
            foreach (var kv in _combatMap)
            {
                var mate = kv.Key;
                var fsm = kv.Value;

                if (mate == null || mate == _bot || mate.IsDead)
                    continue;

                TriggerDelayedFallback(fsm, retreatPoint);
            }
        }

        private static void TriggerDelayedFallback(CombatStateMachine fsm, Vector3 point)
        {
            Task.Run(async () =>
            {
                float delay = UnityEngine.Random.Range(0.15f, 0.4f);
                await Task.Delay((int)(delay * 1000f));
                fsm.TriggerFallback(point);
            });
        }

        #endregion

        #region Squad Voice

        public static void BroadcastMissionType(BotOwner bot, MissionType mission)
        {
            if (bot == null || bot.IsDead || FikaHeadlessDetector.IsHeadless || bot.GetPlayer?.AIData == null)
                return;

            var group = bot.BotsGroup;
            if (group == null)
                return;

            for (int i = 0; i < group.MembersCount; i++)
            {
                var mate = group.Member(i);
                if (mate != null && mate != bot && !mate.IsDead && mate.GetPlayer?.AIData != null)
                {
                    mate.BotTalk?.TrySay(EPhraseTrigger.Cooperation);
                }
            }
        }

        #endregion
    }
}
