#nullable enable

using MissionType = AIRefactored.AI.Missions.BotMissionController.MissionType;

namespace AIRefactored.AI.Groups
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using AIRefactored.AI.Combat;
    using AIRefactored.AI.Helpers;
    using AIRefactored.Core;

    using EFT;

    using UnityEngine;

    using Random = UnityEngine.Random;

    /// <summary>
    ///     Handles squad-level tactical coordination:
    ///     Enemy sharing, fallback broadcast, regrouping, and mission voice triggers.
    /// </summary>
    public sealed class BotTeamLogic
    {
        private const float RegroupJitterRadius = 1.5f;

        private const float RegroupThreshold = 2.5f;

        private readonly BotOwner _bot;

        private readonly Dictionary<BotOwner, CombatStateMachine> _combatMap = new(8);

        private readonly List<BotOwner> _teammates = new(8);

        public BotTeamLogic(BotOwner bot)
        {
            this._bot = bot ?? throw new ArgumentNullException(nameof(bot));
        }

        public static void AddEnemy(BotOwner bot, IPlayer target)
        {
            if (bot == null || bot.IsDead || target == null || bot.BotsGroup == null || bot.Memory == null)
                return;

            var resolved = GameWorldHandler.Get()?.GetAlivePlayerByProfileID(target.ProfileId ?? string.Empty);
            if (resolved == null)
                return;

            for (var i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                var mate = bot.BotsGroup.Member(i);
                if (mate != null && mate != bot && !mate.IsDead && mate.BotsGroup != null && mate.Memory != null)
                    ForceRegisterEnemy(mate, resolved);
            }
        }

        public static void BroadcastMissionType(BotOwner bot, MissionType mission)
        {
            if (bot == null || bot.IsDead || FikaHeadlessDetector.IsHeadless)
                return;

            var group = bot.BotsGroup;
            if (group == null)
                return;

            for (var i = 0; i < group.MembersCount; i++)
            {
                var mate = group.Member(i);
                if (mate != null && mate != bot && !mate.IsDead && mate.GetPlayer?.IsAI == true)
                    mate.BotTalk?.TrySay(EPhraseTrigger.Cooperation);
            }
        }

        public void BroadcastFallback(Vector3 retreatPoint)
        {
            foreach (var kv in this._combatMap)
            {
                var mate = kv.Key;
                var fsm = kv.Value;

                if (mate != null && mate != this._bot && !mate.IsDead)
                    TriggerDelayedFallback(fsm, retreatPoint);
            }
        }

        public void CoordinateMovement()
        {
            if (this._bot.IsDead || this._teammates.Count == 0)
                return;

            var center = Vector3.zero;
            var count = 0;

            foreach (var mate in this._teammates)
                if (mate != null && !mate.IsDead)
                {
                    center += mate.Position;
                    count++;
                }

            if (count == 0)
                return;

            center /= count;
            var jitter = Random.insideUnitSphere * RegroupJitterRadius;
            jitter.y = 0f;

            var target = center + jitter;
            var distSq = (this._bot.Position - target).sqrMagnitude;

            if (distSq > RegroupThreshold * RegroupThreshold)
                BotMovementHelper.SmoothMoveTo(this._bot, target, false);
        }

        public void InjectCombatState(BotOwner mate, CombatStateMachine fsm)
        {
            if (mate != null && fsm != null && mate != this._bot && !this._combatMap.ContainsKey(mate))
                this._combatMap[mate] = fsm;
        }

        public void SetTeammates(List<BotOwner> allBots)
        {
            this._teammates.Clear();

            var player = this._bot.GetPlayer;
            if (player == null || player.Profile?.Info == null)
                return;

            var groupId = player.Profile.Info.GroupId;
            if (string.IsNullOrEmpty(groupId))
                return;

            foreach (var other in allBots)
            {
                if (other == null || other == this._bot || other.IsDead)
                    continue;

                var otherPlayer = other.GetPlayer;
                if (otherPlayer == null || !otherPlayer.IsAI)
                    continue;

                var otherInfo = otherPlayer.Profile?.Info;
                if (otherInfo != null && otherInfo.GroupId == groupId) this._teammates.Add(other);
            }
        }

        public void ShareTarget(IPlayer enemy)
        {
            if (enemy == null || string.IsNullOrEmpty(enemy.ProfileId))
                return;

            var resolved = GameWorldHandler.Get()?.GetAlivePlayerByProfileID(enemy.ProfileId);
            if (resolved == null)
                return;

            foreach (var mate in this._teammates)
                ForceRegisterEnemy(mate, resolved);
        }

        private static void ForceRegisterEnemy(BotOwner receiver, IPlayer enemy)
        {
            if (receiver == null || receiver.IsDead || enemy == null)
                return;

            if (!receiver.BotsGroup.IsEnemy(enemy))
                receiver.BotsGroup.AddEnemy(enemy, EBotEnemyCause.zryachiyLogic);

            if (!receiver.EnemiesController.EnemyInfos.ContainsKey(enemy))
            {
                var settings = new BotSettingsClass(enemy as Player, receiver.BotsGroup, EBotEnemyCause.zryachiyLogic);
                receiver.Memory.AddEnemy(enemy, settings, false);
            }
        }

        private static void TriggerDelayedFallback(CombatStateMachine fsm, Vector3 point)
        {
            Task.Run(async () =>
                {
                    await Task.Delay(Random.Range(150, 400));
                    fsm.TriggerFallback(point);
                });
        }
    }
}