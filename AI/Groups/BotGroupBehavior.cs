#nullable enable

using AIRefactored.AI.Helpers;
using EFT;
using EFT.Interactive;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Controls bot squad cohesion, spacing, and smart door unlocking within a group.
    /// Managed by BotBrain for squad-level coordination.
    /// </summary>
    public class BotGroupBehavior
    {
        private readonly BotOwner _bot;
        private readonly string _groupId;

        private float _doorTimer = 0f;
        private const float DoorCheckInterval = 1.25f;

        private const float SQUAD_SPACING_MIN = 3f;
        private const float SQUAD_SPACING_MAX = 15f;
        private const float LOCKED_DOOR_RADIUS = 2f;

        public BotGroupBehavior(BotOwner bot)
        {
            _bot = bot;
            _groupId = bot.Profile?.Info?.GroupId ?? string.Empty;

            if (_bot.GetPlayer?.IsAI == true && !string.IsNullOrEmpty(_groupId))
                BotTeamTracker.Register(_groupId, _bot);
        }

        public void Dispose()
        {
            if (_bot.GetPlayer?.IsAI == true)
                BotTeamTracker.Unregister(_bot);
        }

        public void Tick(float deltaTime)
        {
            if (!IsValid())
                return;

            if (_bot.Memory?.GoalEnemy != null)
                return;

            HandleSquadSpacing();

            _doorTimer += deltaTime;
            if (_doorTimer >= DoorCheckInterval)
            {
                TryUnlockNearbyDoors();
                _doorTimer = 0f;
            }
        }

        private bool IsValid()
        {
            if (_bot == null || _bot.IsDead)
                return false;

            var player = _bot.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer && !string.IsNullOrEmpty(_groupId);
        }

        private void HandleSquadSpacing()
        {
            List<BotOwner>? group = BotTeamTracker.GetGroup(_groupId);
            if (group == null || group.Count <= 1)
                return;

            int selfIndex = group.IndexOf(_bot);
            if (selfIndex == -1)
                return;

            Vector3 selfPos = _bot.Position;

            for (int i = 0; i < group.Count; i++)
            {
                BotOwner teammate = group[i];
                if (teammate == null || teammate == _bot || teammate.IsDead)
                    continue;

                float dist = Vector3.Distance(selfPos, teammate.Position);

                if (dist < SQUAD_SPACING_MIN)
                {
                    Vector3 stagger = Random.insideUnitSphere * 1.5f;
                    stagger.y = 0f;
                    BotMovementHelper.SmoothMoveTo(_bot, selfPos + stagger, true, 1f);
                }
                else if (dist > SQUAD_SPACING_MAX && selfIndex > i)
                {
                    Vector3 midpoint = Vector3.Lerp(selfPos, teammate.Position, 0.5f);
                    BotMovementHelper.SmoothMoveTo(_bot, midpoint, false, 1f);
                }
            }
        }

        private void TryUnlockNearbyDoors()
        {
            if (_bot.Mover == null)
                return;

            Collider[] hits = Physics.OverlapSphere(_bot.Position, LOCKED_DOOR_RADIUS);
            for (int i = 0; i < hits.Length; i++)
            {
                Door? door = hits[i].GetComponent<Door>();
                if (door != null && (door.DoorState == EDoorState.Shut || door.DoorState == EDoorState.Locked))
                {
                    door.Interact(new InteractionResult(EInteractionType.Open));
                    break;
                }
            }
        }
    }
}
