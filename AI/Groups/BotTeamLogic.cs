#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Missions;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Coordinates team behavior for bots in the same squad, including enemy sharing and squad movement cohesion.
    /// </summary>
    public class BotTeamLogic
    {
        private readonly BotOwner _bot;
        private readonly List<BotOwner> _teammates = new();

        public BotTeamLogic(BotOwner bot)
        {
            _bot = bot;
        }

        /// <summary>
        /// Gathers all alive teammates in the same group.
        /// </summary>
        public void SetTeammates(List<BotOwner> allBots)
        {
            _teammates.Clear();

            if (_bot.GetPlayer?.IsAI != true)
                return;

            string? groupId = _bot.GetPlayer.Profile?.Info?.GroupId;
            if (string.IsNullOrEmpty(groupId))
                return;

            foreach (var other in allBots)
            {
                if (other == null || other == _bot || other.IsDead)
                    continue;

                if (other.GetPlayer?.IsAI != true)
                    continue;

                string? otherGroupId = other.GetPlayer.Profile?.Info?.GroupId;
                if (otherGroupId == groupId)
                    _teammates.Add(other);
            }
        }

        /// <summary>
        /// Share an enemy target with all teammates in memory and group systems.
        /// </summary>
        public void ShareTarget(IPlayer enemy)
        {
            if (enemy == null || string.IsNullOrEmpty(enemy.ProfileId))
                return;

            var resolved = Singleton<GameWorld>.Instance?.GetAlivePlayerByProfileID(enemy.ProfileId);
            if (resolved == null)
                return;

            foreach (var teammate in _teammates)
            {
                if (teammate == null || teammate.IsDead || teammate.GetPlayer?.IsAI != true)
                    continue;

                if (teammate.Memory?.GoalEnemy?.Person?.Id == resolved.Id)
                    continue;

                if (!teammate.BotsGroup?.IsEnemy(resolved) ?? true)
                {
                    if (!teammate.BotsGroup!.AddEnemy(resolved, EBotEnemyCause.zryachiyLogic))
                        continue;
                }

                if (teammate.BotsGroup.Enemies.TryGetValue(resolved, out var info))
                    teammate.Memory?.AddEnemy(resolved, info, false);
            }
        }

        /// <summary>
        /// Coordinates movement with teammates by forming a staggered center point regroup.
        /// </summary>
        public void CoordinateMovement()
        {
            if (_bot.GetPlayer?.IsAI != true || _bot.IsDead || _teammates.Count == 0)
                return;

            Vector3 center = Vector3.zero;
            int count = 0;

            foreach (var mate in _teammates)
            {
                if (mate == null || mate.IsDead || mate.GetPlayer?.IsAI != true)
                    continue;

                center += mate.Position;
                count++;
            }

            if (count == 0)
                return;

            center /= count;
            Vector3 stagger = Random.insideUnitSphere * 1.5f;
            stagger.y = 0f;
            Vector3 regroupPoint = center + stagger;

            if (Vector3.Distance(_bot.Position, regroupPoint) > 2f)
                BotMovementHelper.SmoothMoveTo(_bot, regroupPoint, false, 1f);
        }

        /// <summary>
        /// Shares a detected enemy with the entire group (static call).
        /// </summary>
        public static void AddEnemy(BotOwner bot, IPlayer target)
        {
            if (bot?.BotsGroup == null || target == null || bot.IsDead)
                return;

            for (int i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                var teammate = bot.BotsGroup.Member(i);
                if (teammate == null || teammate.IsDead || teammate == bot || teammate.GetPlayer?.IsAI != true)
                    continue;

                if (teammate.Memory?.GoalEnemy?.Person?.Id == target.Id)
                    continue;

                if (!teammate.BotsGroup?.IsEnemy(target) ?? true)
                {
                    if (!teammate.BotsGroup!.AddEnemy(target, EBotEnemyCause.zryachiyLogic))
                        continue;
                }

                if (teammate.BotsGroup.Enemies.TryGetValue(target, out var info))
                    teammate.Memory?.AddEnemy(target, info, false);
            }
        }

        /// <summary>
        /// Broadcasts a fallback location to nearby squadmates.
        /// </summary>
        public static void BroadcastFallback(BotOwner bot, Vector3 retreatPoint)
        {
            if (bot?.BotsGroup == null || bot.IsDead)
                return;

            for (int i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                var teammate = bot.BotsGroup.Member(i);
                if (teammate == null || teammate.IsDead || teammate == bot)
                    continue;

                var sm = teammate.GetComponent<CombatStateMachine>();
                sm?.TriggerFallback(retreatPoint);
            }
        }

        /// <summary>
        /// Announces a mission shift (e.g. loot, extract, defend) to the team.
        /// </summary>
        public static void BroadcastMissionType(BotOwner bot, BotMissionSystem.MissionType mission)
        {
            if (bot?.BotsGroup == null || bot.IsDead)
                return;

            for (int i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                var teammate = bot.BotsGroup.Member(i);
                if (teammate == null || teammate == bot || teammate.IsDead)
                    continue;

                var brain = teammate.GetComponent<BotMissionSystem>();
                if (brain != null)
                    teammate.BotTalk?.TrySay(EPhraseTrigger.Cooperation);
            }
        }
    }
}
