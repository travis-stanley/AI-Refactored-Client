#nullable enable

using EFT;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Controls how a bot moves and behaves relative to its group.
    /// Used for maintaining squad cohesion and spacing during patrol or fallback.
    /// </summary>
    public class BotGroupBehavior : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotComponentCache? _cache;
        private BotsGroup? _group;

        private const float SpacingMin = 2.5f;
        private const float SpacingMax = 8f;
        private const float MoveCohesion = 1.0f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _group = _bot?.BotsGroup;
        }

        public void Tick(float deltaTime)
        {
            if (_bot == null || _group == null || _bot.IsDead)
                return;

            if (_bot.Memory?.GoalEnemy != null)
                return; // In combat — let combat logic handle positioning

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
