#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Controls how a bot moves and behaves relative to its group.
    /// Used for maintaining squad cohesion and spacing during patrol or fallback.
    /// </summary>
    public class BotGroupBehavior
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotsGroup? _group;

        private const float SpacingMin = 2.5f;
        private const float SpacingMax = 8f;
        private const float MoveCohesion = 1.0f;

        /// <summary>
        /// Initializes the group behavior using the shared bot cache.
        /// </summary>
        public void Initialize(BotComponentCache cache)
        {
            _cache = cache;
            _bot = cache.Bot;
            _group = _bot?.BotsGroup;
        }

        /// <summary>
        /// Called every frame to adjust bot's movement relative to group members.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (_bot == null || _group == null || _bot.IsDead)
                return;

            if (_bot.Memory?.GoalEnemy != null)
                return; // Let combat state handle movement during fight

            for (int i = 0; i < _group.MembersCount; i++)
            {
                var mate = _group.Member(i);
                if (mate == null || mate == _bot || mate.IsDead)
                    continue;

                float dist = Vector3.Distance(_bot.Position, mate.Position);

                if (dist < SpacingMin)
                {
                    // Too close — stagger slightly apart
                    Vector3 direction = (_bot.Position - mate.Position).normalized;
                    Vector3 target = _bot.Position + direction * 2.0f;
                    BotMovementHelper.SmoothMoveTo(_bot, target, false, MoveCohesion);
                    return;
                }

                if (dist > SpacingMax && mate.Memory?.GoalEnemy == null)
                {
                    // Too far — regroup with the teammate if not in combat
                    Vector3 direction = (mate.Position - _bot.Position).normalized;
                    Vector3 target = _bot.Position + direction * 4.0f;
                    BotMovementHelper.SmoothMoveTo(_bot, target, false, MoveCohesion);
                    return;
                }
            }
        }
    }
}
