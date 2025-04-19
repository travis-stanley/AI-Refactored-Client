#nullable enable

using EFT;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Controls how a bot moves and behaves relative to its group.
    /// Used for maintaining squad cohesion and spacing during patrol or fallback.
    /// </summary>
    public class BotGroupBehavior : MonoBehaviour
    {
        private BotOwner? _bot;
        private BotsGroup? _group;

        private const float SpacingMin = 2.5f;
        private const float SpacingMax = 8f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _group = _bot?.BotsGroup;
        }

        public void Tick(float deltaTime)
        {
            if (_bot == null || _group == null || _bot.IsDead)
                return;

            if (_bot.Memory?.GoalEnemy != null)
                return; // In combat, skip cohesion logic

            for (int i = 0; i < _group.MembersCount; i++)
            {
                var mate = _group.Member(i);
                if (mate == null || mate == _bot)
                    continue;

                float dist = Vector3.Distance(_bot.Position, mate.Position);

                if (dist < SpacingMin)
                {
                    // Too close — stagger apart slightly
                    Vector3 away = (_bot.Position - mate.Position).normalized;
                    Vector3 target = _bot.Position + away * 2f;
                    _bot.Mover?.GoToPoint(target, slowAtTheEnd: false, reachDist: 1f);
                    return;
                }

                if (dist > SpacingMax && mate.Memory?.GoalEnemy == null)
                {
                    // Too far from squadmate — regroup if not fighting
                    Vector3 toward = (mate.Position - _bot.Position).normalized;
                    Vector3 target = _bot.Position + toward * 4f;
                    _bot.Mover?.GoToPoint(target, slowAtTheEnd: false, reachDist: 1f);
                    return;
                }
            }
        }
    }
}
