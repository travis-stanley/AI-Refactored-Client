#nullable enable

using System.Collections.Generic;
using AIRefactored.AI;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using Comfort.Common;
using EFT;
using EFT.Interactive;
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

            var player = _bot.GetPlayer;
            if (player == null || !player.IsAI)
                return;

            string? myGroupId = player.Profile?.Info?.GroupId;
            if (string.IsNullOrEmpty(myGroupId))
                return;

            foreach (var other in allBots)
            {
                if (other == null || other == _bot)
                    continue;

                var otherPlayer = other.GetPlayer;
                if (otherPlayer == null || !otherPlayer.IsAI)
                    continue;

                string? otherGroupId = otherPlayer.Profile?.Info?.GroupId;
                if (!string.IsNullOrEmpty(otherGroupId) && otherGroupId == myGroupId)
                {
                    _teammates.Add(other);
                }
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

            for (int i = 0; i < _teammates.Count; i++)
            {
                var teammate = _teammates[i];
                var teammatePlayer = teammate?.GetPlayer;

                if (teammate == null || teammatePlayer == null || !teammatePlayer.IsAI)
                    continue;

                if (teammate.Memory == null || teammate.BotsGroup == null || !teammate.HealthController.IsAlive)
                    continue;

                bool alreadyTargeting = teammate.Memory.GoalEnemy?.Person?.Id == resolved.Id;
                if (alreadyTargeting)
                    continue;

                if (!teammate.BotsGroup.IsEnemy(resolved))
                {
                    bool success = teammate.BotsGroup.AddEnemy(resolved, EBotEnemyCause.zryachiyLogic);
                    if (!success)
                        continue;
                }

                if (teammate.BotsGroup.Enemies.TryGetValue(resolved, out var groupInfo))
                {
                    teammate.Memory.AddEnemy(resolved, groupInfo, onActivation: false);
                }
            }
        }

        #endregion

        #region Coordination

        /// <summary>
        /// Aligns the bot with the average squad position smoothly, avoiding teleportation or tick-gated motion.
        /// </summary>
        public void CoordinateMovement()
        {
            var player = _bot.GetPlayer;
            if (player == null || !player.IsAI)
                return;

            if (_teammates.Count == 0 || _bot.IsDead)
                return;

            Vector3 center = Vector3.zero;
            int count = 0;

            for (int i = 0; i < _teammates.Count; i++)
            {
                var teammate = _teammates[i];
                var teammatePlayer = teammate?.GetPlayer;

                if (teammate != null &&
                    teammatePlayer != null &&
                    teammatePlayer.IsAI &&
                    teammatePlayer.HealthController.IsAlive)
                {
                    center += teammate.Position;
                    count++;
                }
            }

            if (count > 0)
            {
                center /= count;

                Vector3 stagger = Random.insideUnitSphere * 1.5f;
                stagger.y = 0f;

                Vector3 regroupPoint = center + stagger;
                float distance = Vector3.Distance(_bot.Position, regroupPoint);

                if (distance > 2f)
                {
                    BotMovementHelper.SmoothMoveTo(_bot, regroupPoint, false, cohesionScale: 1f);
                }
            }
        }

        #endregion
    }
}
