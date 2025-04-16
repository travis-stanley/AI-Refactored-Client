#nullable enable

using EFT;
using System.Collections.Generic;
using UnityEngine;
using Comfort.Common;

namespace AIRefactored.AI.Group
{
    /// <summary>
    /// Handles coordinated bot behavior such as enemy sharing and squad cohesion.
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
        /// Scans and stores teammates from all known bots based on matching GroupId.
        /// </summary>
        public void SetTeammates(List<BotOwner> allBots)
        {
            _teammates.Clear();

            string? myGroupId = _bot?.GetPlayer?.Profile?.Info?.GroupId;
            if (string.IsNullOrEmpty(myGroupId))
                return;

            foreach (var other in allBots)
            {
                if (other == null || other == _bot)
                    continue;

                string? otherGroupId = other.GetPlayer?.Profile?.Info?.GroupId;
                if (!string.IsNullOrEmpty(otherGroupId) && otherGroupId == myGroupId)
                {
                    _teammates.Add(other);
                }
            }
        }

        /// <summary>
        /// Shares a seen enemy with squadmates, adding them to memory and combat state.
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
                if (teammate == null || teammate.Memory == null || teammate.BotsGroup == null || !teammate.HealthController.IsAlive)
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

        /// <summary>
        /// Attempts to regroup the bot with teammates by calculating the center of the squad.
        /// </summary>
        public void CoordinateMovement()
        {
            if (_teammates.Count == 0 || _bot.Mover == null || _bot.IsDead)
                return;

            Vector3 center = Vector3.zero;
            int count = 0;

            for (int i = 0; i < _teammates.Count; i++)
            {
                var teammate = _teammates[i];
                if (teammate?.GetPlayer != null && teammate.GetPlayer.HealthController.IsAlive)
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
                    _bot.Mover.GoToPoint(regroupPoint, slowAtTheEnd: false, reachDist: 1f, getUpWithCheck: true);
                }
            }
        }
    }
}
