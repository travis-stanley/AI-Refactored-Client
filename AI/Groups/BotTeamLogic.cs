#nullable enable

using AIRefactored.AI.Combat;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Missions;
using AIRefactored.Core;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Provides tactical group logic for bots in the same squad, including enemy sharing, fallback, and regrouping.
    /// </summary>
    public class BotTeamLogic
    {
        #region Fields

        private readonly BotOwner _bot;
        private readonly List<BotOwner> _teammates = new();

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new BotTeamLogic instance for the given bot.
        /// </summary>
        /// <param name="bot">The bot owner associated with this logic controller.</param>
        public BotTeamLogic(BotOwner bot)
        {
            _bot = bot;
        }

        #endregion

        #region Team Synchronization

        /// <summary>
        /// Populates the internal teammate list based on all known AI bots that share the same group ID.
        /// </summary>
        /// <param name="allBots">A list of all active bot owners.</param>
        public void SetTeammates(List<BotOwner> allBots)
        {
            _teammates.Clear();

            var player = _bot.GetPlayer;
            if (player == null || !player.IsAI)
                return;

            string? groupId = player.Profile?.Info?.GroupId;
            if (string.IsNullOrEmpty(groupId))
                return;

            for (int i = 0; i < allBots.Count; i++)
            {
                var other = allBots[i];
                if (other == null || other == _bot || other.IsDead)
                    continue;

                var otherPlayer = other.GetPlayer;
                if (otherPlayer?.IsAI != true)
                    continue;

                string? otherGroupId = otherPlayer.Profile?.Info?.GroupId;
                if (otherGroupId == groupId)
                    _teammates.Add(other);
            }
        }

        #endregion

        #region Enemy Sharing

        /// <summary>
        /// Pushes a known enemy to all current teammates, triggering group targeting logic and memory update.
        /// </summary>
        /// <param name="enemy">The IPlayer enemy to share with the group.</param>
        public void ShareTarget(IPlayer enemy)
        {
            if (enemy == null || string.IsNullOrEmpty(enemy.ProfileId))
                return;

            var resolved = GameWorldHandler.Get()?.GetAlivePlayerByProfileID(enemy.ProfileId);
            if (resolved == null)
                return;

            for (int i = 0; i < _teammates.Count; i++)
            {
                var teammate = _teammates[i];
                if (teammate == null || teammate.IsDead || teammate.GetPlayer?.IsAI != true)
                    continue;

                if (teammate.Memory?.GoalEnemy?.Person?.Id == resolved.Id)
                    continue;

                if (!teammate.BotsGroup?.IsEnemy(resolved) ?? true)
                {
                    if (!teammate.BotsGroup!.AddEnemy(resolved, EBotEnemyCause.zryachiyLogic))
                        continue;
                }

                var enemies = teammate.BotsGroup?.Enemies;
                if (enemies != null && enemies.TryGetValue(resolved, out var info))
                {
                    teammate.Memory?.AddEnemy(resolved, info, false);
                }
            }
        }

        /// <summary>
        /// Static helper to add a shared enemy across all squad members from the given bot’s group.
        /// </summary>
        /// <param name="bot">The bot who initially saw the enemy.</param>
        /// <param name="target">The IPlayer target to sync.</param>
        public static void AddEnemy(BotOwner bot, IPlayer target)
        {
            if (bot?.BotsGroup == null || target == null || bot.IsDead)
                return;

            var group = bot.BotsGroup;

            for (int i = 0; i < group.MembersCount; i++)
            {
                var teammate = group.Member(i);
                if (teammate == null || teammate.IsDead || teammate == bot || teammate.GetPlayer?.IsAI != true)
                    continue;

                if (teammate.Memory?.GoalEnemy?.Person?.Id == target.Id)
                    continue;

                if (!teammate.BotsGroup?.IsEnemy(target) ?? true)
                {
                    if (!teammate.BotsGroup!.AddEnemy(target, EBotEnemyCause.zryachiyLogic))
                        continue;
                }

                var enemies = teammate.BotsGroup?.Enemies;
                if (enemies != null && enemies.TryGetValue(target, out var info))
                {
                    teammate.Memory?.AddEnemy(target, info, false);
                }
            }
        }

        #endregion

        #region Squad Movement

        /// <summary>
        /// Moves this bot toward the average squad center position with slight randomization.
        /// </summary>
        public void CoordinateMovement()
        {
            if (_bot.GetPlayer?.IsAI != true || _bot.IsDead || _teammates.Count == 0)
                return;

            Vector3 center = Vector3.zero;
            int count = 0;

            for (int i = 0; i < _teammates.Count; i++)
            {
                var mate = _teammates[i];
                if (mate == null || mate.IsDead || mate.GetPlayer?.IsAI != true)
                    continue;

                center += mate.Position;
                count++;
            }

            if (count == 0)
                return;

            center /= count;

            Vector3 offset = UnityEngine.Random.insideUnitSphere * 1.5f;
            offset.y = 0f;
            Vector3 regroupPoint = center + offset;

            if (Vector3.Distance(_bot.Position, regroupPoint) > 2.5f)
            {
                BotMovementHelper.SmoothMoveTo(_bot, regroupPoint, false, 1.0f);
            }
        }

        /// <summary>
        /// Instructs all squadmates to fallback to a shared retreat location.
        /// </summary>
        /// <param name="bot">The bot initiating the fallback.</param>
        /// <param name="retreatPoint">The position to fall back to.</param>
        public static void BroadcastFallback(BotOwner bot, Vector3 retreatPoint)
        {
            if (bot?.BotsGroup == null || bot.IsDead)
                return;

            for (int i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                var teammate = bot.BotsGroup.Member(i);
                if (teammate == null || teammate.IsDead || teammate == bot)
                    continue;

                var stateMachine = teammate.GetComponent<CombatStateMachine>();
                stateMachine?.TriggerFallback(retreatPoint);
            }
        }

        #endregion

        #region Mission Coordination

        /// <summary>
        /// Announces the mission type to squadmates and optionally plays cooperation VO.
        /// </summary>
        /// <param name="bot">The broadcasting bot.</param>
        /// <param name="mission">The mission type (Quest, Loot, etc).</param>
        public static void BroadcastMissionType(BotOwner bot, BotMissionSystem.MissionType mission)
        {
            if (bot?.BotsGroup == null || bot.IsDead)
                return;

            for (int i = 0; i < bot.BotsGroup.MembersCount; i++)
            {
                var teammate = bot.BotsGroup.Member(i);
                if (teammate == null || teammate == bot || teammate.IsDead)
                    continue;

                if (teammate.GetPlayer?.IsAI == true && !FikaHeadlessDetector.IsHeadless)
                {
                    teammate.BotTalk?.TrySay(EPhraseTrigger.Cooperation);
                }
            }
        }

        #endregion
    }
}
