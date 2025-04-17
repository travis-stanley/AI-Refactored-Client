#nullable enable

using EFT;
using System.Collections.Generic;
using UnityEngine;
using Comfort.Common;

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

        /// <summary>
        /// Creates a new team logic manager for a specific bot.
        /// </summary>
        public BotTeamLogic(BotOwner bot)
        {
            _bot = bot;
        }

        #endregion

        #region Team Setup

        /// <summary>
        /// Filters and sets valid AI teammates from a list of all bots, based on GroupId.
        /// </summary>
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

        /// <summary>
        /// Shares an enemy target with squadmates, updating group memory and enemy lists.
        /// </summary>
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
        /// Moves the bot toward the average position of the squad, with slight jitter for spacing.
        /// </summary>
        public void CoordinateMovement()
        {
            var player = _bot.GetPlayer;
            if (player == null || !player.IsAI)
                return;

            if (_teammates.Count == 0 || _bot.Mover == null || _bot.IsDead)
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

                Vector3 stagger = Random.insideUnitSphere * 2f;
                stagger.y = 0f;

                Vector3 regroupPoint = center + stagger;

                if (Vector3.Distance(_bot.Position, regroupPoint) > 4f)
                {
                    _bot.Mover.GoToPoint(
                        regroupPoint,
                        slowAtTheEnd: false,
                        reachDist: 1f,
                        getUpWithCheck: true
                    );
                }
            }
        }

        #endregion
    }
}
