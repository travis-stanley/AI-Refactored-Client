#nullable enable

using AIRefactored.AI.Helpers;
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

        /// <summary>
        /// Initializes team logic for a specific bot.
        /// </summary>
        public BotTeamLogic(BotOwner bot)
        {
            _bot = bot;
        }

        #endregion

        #region Team Setup

        /// <summary>
        /// Filters and stores teammates based on shared GroupId.
        /// </summary>
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

        /// <summary>
        /// Shares a detected enemy with all squadmates who haven’t seen them yet.
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

        #endregion

        #region Coordination

        /// <summary>
        /// Smoothly aligns bot with average group position. Adds natural stagger and avoids tight clustering.
        /// </summary>
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
    }
}
