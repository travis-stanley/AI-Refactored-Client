#nullable enable

using System;
using UnityEngine;
using EFT;
using EFT.Interactive;
using AIRefactored.AI.Core;
using AIRefactored.AI.Combat;
using System.Collections.Generic;

namespace AIRefactored.AI.Movement
{
    public class BotMovementController : MonoBehaviour
    {
        private BotOwner _bot = null!;
        private AIRefactoredBotOwner _owner = null!;
        private BotComponentCache _cache = null!;
        private BotMover _mover = null!;
        private BotLay _lay = null!;
        private BotAssaultBuildingData _assaultData = null!;
        private BotDoorOpener _doorOpener = null!;
        private BotPanicHandler _panicHandler = null!;
        private bool IsPanicking => _panicHandler != null && _panicHandler.enabled;

        private float _nextDecisionTime;
        private const float DecisionInterval = 0.3f;
        private int _currentRoomIndex = 0;
        private int _groupSlotIndex = -1;
        private float _nextTriggerCheck = 0f;

        private void Awake()
        {
            _bot = GetComponent<BotOwner>();
            _owner = GetComponent<AIRefactoredBotOwner>();
            _cache = GetComponent<BotComponentCache>();
            _mover = _bot.Mover;
            _lay = _bot.BotLay;
            _assaultData = _bot.AssaultBuildingData;
            _doorOpener = _bot.DoorOpener;
            _panicHandler = GetComponent<BotPanicHandler>();

            if (_bot.BotsGroup != null && _bot.BotsGroup._members != null)
                _groupSlotIndex = _bot.BotsGroup._members.IndexOf(_bot);
        }

        private void Update()
        {
            if (_bot == null || _owner == null || _mover == null || _cache == null || _bot.IsDead || Time.time < _nextDecisionTime)
                return;

            _nextDecisionTime = Time.time + DecisionInterval;

            if (IsPanicking)
                return;

            UpdateMovementBehavior();

            if (Time.time > _nextTriggerCheck)
            {
                _nextTriggerCheck = Time.time + 0.5f;
                CheckIndoorTriggerZones();
            }
        }

        private void UpdateMovementBehavior()
        {
            if (TryRoomSearchFlow()) return;
            if (TrySuppressOrAmbush()) return;
        }

        private void CheckIndoorTriggerZones()
        {
            Collider[] zones = Physics.OverlapSphere(_bot.Position, 1.25f, LayerMask.GetMask("Trigger"));
            foreach (var zone in zones)
            {
                if (zone.name.ToLower().Contains("choke") || zone.name.ToLower().Contains("ambush"))
                {
                    if (_owner.PersonalityProfile.Caution > 0.6f && _lay.CanProne && !_bot.Memory.HaveEnemy)
                    {
                        _lay.TryLay();
                        _mover.SetTargetMoveSpeed(0f);
                        break;
                    }
                }
            }
        }

        private bool TryRoomSearchFlow()
        {
            var places = _bot.BotsGroup?.PlacesForCheck;
            if (places == null || places.Count == 0)
                return false;

            int offset = (_groupSlotIndex >= 0) ? _groupSlotIndex % places.Count : 0;
            int index = (_currentRoomIndex + offset) % places.Count;
            Vector3 target = places[index].Position;

            if (Vector3.Distance(_bot.Position, target) > 2f)
            {
                _mover.SetTargetMoveSpeed(0.4f);
                _bot.GoToPoint(target, false);
            }
            else
            {
                _currentRoomIndex = (_currentRoomIndex + 1) % places.Count;
            }

            return true;
        }

        private bool TrySuppressOrAmbush()
        {
            if (_owner.PersonalityProfile.Caution > 0.75f &&
                _cache.LastHeardTime + 6f > Time.time &&
                !_bot.Memory.HaveEnemy &&
                _lay != null && _lay.CanProne)
            {
                _lay.TryLay();
                _mover.SetTargetMoveSpeed(0f);
                return true;
            }

            return false;
        }
    }
}
