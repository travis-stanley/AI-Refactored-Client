#nullable enable

using System.Collections.Generic;
using AIRefactored.AI;
using AIRefactored.AI.Groups;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using UnityEngine;

namespace AIRefactored.AI.Groups
{
    public class BotGroupBehavior : MonoBehaviour
    {
        private BotOwner? _bot;
        private string? _groupId;

        private float updateInterval = 1.25f;
        private float updateTimer = 0f;

        private const float SQUAD_SPACING_MIN = 3f;
        private const float SQUAD_SPACING_MAX = 15f;
        private const float LOCKED_DOOR_RADIUS = 2f;

        private void Start()
        {
            _bot = GetComponent<BotOwner>();
            _groupId = _bot?.Profile?.Info?.GroupId;

            if (_bot != null && !string.IsNullOrEmpty(_groupId))
            {
                BotTeamTracker.Register(_groupId, _bot);
            }
        }

        private void OnDestroy()
        {
            if (_bot != null)
                BotTeamTracker.Unregister(_bot);
        }

        private void Update()
        {
            if (_bot == null || _bot.IsDead || string.IsNullOrEmpty(_groupId))
                return;

            updateTimer += Time.deltaTime;
            if (updateTimer < updateInterval)
                return;

            updateTimer = 0f;

            if (_bot.Memory?.GoalEnemy != null)
                return;

            List<BotOwner> group = BotTeamTracker.GetGroup(_groupId);
            if (group.Count <= 1)
                return;

            int selfIndex = group.IndexOf(_bot);

            for (int i = 0; i < group.Count; i++)
            {
                var teammate = group[i];
                if (teammate == null || teammate == _bot || teammate.IsDead)
                    continue;

                float dist = Vector3.Distance(_bot.Position, teammate.Position);

                if (dist < SQUAD_SPACING_MIN)
                {
                    Vector3 stagger = Random.insideUnitSphere * 1.5f;
                    stagger.y = 0;
                    _bot.GoToPoint(_bot.Position + stagger, slowAtTheEnd: true);
                }
                else if (dist > SQUAD_SPACING_MAX && selfIndex > i)
                {
                    Vector3 mid = Vector3.Lerp(_bot.Position, teammate.Position, 0.5f);
                    _bot.GoToPoint(mid, slowAtTheEnd: false);
                }
            }

            TryUnlockNearbyDoors();
        }

        private void TryUnlockNearbyDoors()
        {
            if (_bot?.Mover == null)
                return;

            Collider[] hits = Physics.OverlapSphere(_bot.Position, LOCKED_DOOR_RADIUS);
            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out Door door))
                {
                    if (door.DoorState == EDoorState.Shut || door.DoorState == EDoorState.Locked)
                    {
                        door.Interact(new InteractionResult(EInteractionType.Open));

#if UNITY_EDITOR
                        Debug.Log($"[AIRefactored-Group] {_bot.Profile?.Info?.Nickname} unlocked door: {door.name}");
#endif
                        break;
                    }
                }
            }
        }
    }
}
