#nullable enable

using System.Collections.Generic;
using AIRefactored.AI;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Helpers;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    /// <summary>
    /// Controls bot squad cohesion, spacing, and smart door unlocking within a group.
    /// Keeps AI squads tightly aligned without overlapping or separating too far.
    /// </summary>
    public class BotGroupBehavior : MonoBehaviour
    {
        #region Fields

        private BotOwner? _bot;
        private string? _groupId;

        private float doorTimer = 0f;
        private const float DoorCheckInterval = 1.25f;

        private const float SQUAD_SPACING_MIN = 3f;
        private const float SQUAD_SPACING_MAX = 15f;
        private const float LOCKED_DOOR_RADIUS = 2f;

        #endregion

        #region Unity Events

        private void Start()
        {
            _bot = GetComponent<BotOwner>();
            _groupId = _bot?.Profile?.Info?.GroupId;

            if (_bot != null && _bot.GetPlayer?.IsAI == true && !string.IsNullOrEmpty(_groupId))
            {
                BotTeamTracker.Register(_groupId, _bot);
            }
        }

        private void OnDestroy()
        {
            if (_bot != null && _bot.GetPlayer?.IsAI == true)
            {
                BotTeamTracker.Unregister(_bot);
            }
        }

        private void Update()
        {
            if (_bot == null || _bot.IsDead || string.IsNullOrEmpty(_groupId))
                return;

            if (_bot.GetPlayer == null || !_bot.GetPlayer.IsAI)
                return;

            if (_bot.Memory?.GoalEnemy != null)
                return;

            HandleSquadSpacing();

            doorTimer += Time.deltaTime;
            if (doorTimer >= DoorCheckInterval)
            {
                TryUnlockNearbyDoors();
                doorTimer = 0f;
            }
        }

        #endregion

        #region Behavior Logic

        private void HandleSquadSpacing()
        {
            List<BotOwner> group = BotTeamTracker.GetGroup(_groupId!);
            if (group.Count <= 1)
                return;

            int selfIndex = group.IndexOf(_bot!);
            if (selfIndex == -1)
                return;

            for (int i = 0; i < group.Count; i++)
            {
                var teammate = group[i];
                if (teammate == null || teammate == _bot || teammate.IsDead)
                    continue;

                float dist = Vector3.Distance(_bot!.Position, teammate.Position);

                if (dist < SQUAD_SPACING_MIN)
                {
                    Vector3 stagger = Random.insideUnitSphere * 1.5f;
                    stagger.y = 0f;
                    BotMovementHelper.SmoothMoveTo(_bot, _bot.Position + stagger, true, 1f);
                }
                else if (dist > SQUAD_SPACING_MAX && selfIndex > i)
                {
                    Vector3 midpoint = Vector3.Lerp(_bot.Position, teammate.Position, 0.5f);
                    BotMovementHelper.SmoothMoveTo(_bot, midpoint, false, 1f);
                }
            }
        }

        private void TryUnlockNearbyDoors()
        {
            if (_bot?.Mover == null)
                return;

            Collider[] hits = Physics.OverlapSphere(_bot.Position, LOCKED_DOOR_RADIUS);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].TryGetComponent(out Door door))
                {
                    if (door.DoorState == EDoorState.Shut || door.DoorState == EDoorState.Locked)
                    {
                        door.Interact(new InteractionResult(EInteractionType.Open));
                        break;
                    }
                }
            }
        }

        #endregion
    }
}
