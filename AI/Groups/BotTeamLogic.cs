#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Missions;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Group
{
    /// <summary>
    /// Coordinates team behavior for bots in the same squad, including enemy sharing and squad movement cohesion.
    /// </summary>
    public class BotTeamLogic
    {
        #region Fields

        private readonly BotOwner _bot;
        private readonly List<BotOwner> _teammates = new();

        #endregion

        #region Constructor

        public BotTeamLogic(BotOwner bot)
        {
            _bot = bot;
        }

        #endregion

        #region Team Setup

        public void SetTeammates(List<BotOwner> allBots)
        {
            _teammates.Clear();

            if (_bot?.GetPlayer?.IsAI != true)
                return;

            string? myGroupId = _bot.GetPlayer.Profile?.Info?.GroupId;
            if (string.IsNullOrEmpty(myGroupId))
                return;

            foreach (var other in allBots)
            {
                if (other == null || other == _bot || other.IsDead)
                    continue;

                var player = other.GetPlayer;
                if (player?.IsAI != true)
                    continue;

                string? groupId = player.Profile?.Info?.GroupId;
                if (groupId == myGroupId)
                    _teammates.Add(other);
            }
        }

        #endregion

        #region Target Sharing

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

        #endregion

        #region Coordination

        public void CoordinateMovement()
        {
            if (_bot?.GetPlayer?.IsAI != true || _bot.IsDead || _teammates.Count == 0)
                return;

            Vector3 center = Vector3.zero;
            int count = 0;

            foreach (var mate in _teammates)
            {
                if (mate == null || mate.GetPlayer?.IsAI != true || mate.IsDead)
                    continue;

                center += mate.Position;
                count++;
            }

            if (count > 0)
            {
                center /= count;

                Vector3 stagger = Random.insideUnitSphere * 1.5f;
                stagger.y = 0f;

                Vector3 regroupPoint = center + stagger;
                float dist = Vector3.Distance(_bot.Position, regroupPoint);

                if (dist > 2f)
                    BotMovementHelper.SmoothMoveTo(_bot, regroupPoint, false, 1f);
            }
        }

        #endregion

        #region Static Global Methods

        /// <summary>
        /// Shares a detected enemy with all squadmates of the calling bot.
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
        /// Broadcasts a fallback position to all group members.
        /// </summary>
        public static void BroadcastFallback(BotOwner bot, Vector3 retreatPoint)
        {
            if (bot?.BotsGroup == null || bot.IsDead)
                return;

            for (int i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                var teammate = bot.BotsGroup.Member(i);
                if (teammate == null || teammate == bot || teammate.IsDead)
                    continue;

                var machine = teammate.GetComponent<CombatStateMachine>();
                machine?.TriggerFallback(retreatPoint);
            }
        }

        /// <summary>
        /// Informs group members of a mission change. Bots may react (e.g. enter alert or adjust objective).
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

                // Custom logic placeholder — bots could align or comment
                var brain = teammate.GetComponent<BotMissionSystem>();
                if (brain != null)
                {
                    // Future: dynamic sync of objectives, or reaction VO
                    teammate.BotTalk?.TrySay(EPhraseTrigger.Cooperation);
                }
            }
        }


        #endregion
    }
}
