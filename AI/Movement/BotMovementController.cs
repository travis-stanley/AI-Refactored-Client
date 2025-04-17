#nullable enable

using UnityEngine;
using EFT;
using System.Collections;
using System.Collections.Generic;
using EFT.Interactive;
using AIRefactored.Core;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;

namespace AIRefactored.AI.Movement
{
    /// <summary>
    /// Handles tactical room clearing, ambush behavior, and group movement.
    /// Controls bot movement in confined spaces, leaning, scanning, door interaction, and fallback positioning.
    /// </summary>
    public class BotMovementController : MonoBehaviour
    {
        #region Fields

        private BotOwner _bot = null!;
        private AIRefactoredBotOwner _owner = null!;
        private BotComponentCache _cache = null!;
        private BotMover _mover = null!;
        private BotLay _lay = null!;
        private BotAssaultBuildingData _assaultData = null!;
        private BotDoorOpener _doorOpener = null!;
        private BotPanicHandler _panicHandler = null!;

        private float _nextDecisionTime = 0f;
        private float _nextTriggerCheck = 0f;
        private int _currentRoomIndex = 0;
        private int _groupSlotIndex = -1;
        private bool _isScanning = false;

        private const float DecisionInterval = 0.3f;
        private const float TriggerZoneRadius = 1.25f;
        private const float LeanDuration = 0.75f;
        private const float DoorStackWait = 0.5f;

        private static readonly int TriggerLayerMask = LayerMask.GetMask("Trigger");
        private static readonly int LOSLayerMask = LayerMask.GetMask("Default");

        private bool IsPanicking => _panicHandler != null && _panicHandler.enabled;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            _bot = GetComponent<BotOwner>()!;
            _owner = GetComponent<AIRefactoredBotOwner>()!;
            _cache = GetComponent<BotComponentCache>()!;
            _panicHandler = GetComponent<BotPanicHandler>();

            _mover = _bot.Mover;
            _lay = _bot.BotLay;
            _assaultData = _bot.AssaultBuildingData;
            _doorOpener = _bot.DoorOpener;

            if (_bot.BotsGroup?._members != null)
                _groupSlotIndex = _bot.BotsGroup._members.IndexOf(_bot);
        }

        private void Update()
        {
            if (_bot == null || _bot.IsDead || Time.time < _nextDecisionTime)
                return;

            _nextDecisionTime = Time.time + DecisionInterval;

            if (IsPanicking)
                return;

            if (!_isScanning)
                StartCoroutine(DecisionRoutine());

            if (Time.time > _nextTriggerCheck)
            {
                _nextTriggerCheck = Time.time + 0.5f;
                CheckTriggerZones();
            }
        }

        #endregion

        #region Decision Routine

        private IEnumerator DecisionRoutine()
        {
            _isScanning = true;

            if (TrySuppressOrAmbush())
            {
                _isScanning = false;
                yield break;
            }

            if (TryRoomSearchFlow())
            {
                _isScanning = false;
                yield break;
            }

            _isScanning = false;
        }

        #endregion

        #region Trigger Zones

        private void CheckTriggerZones()
        {
            if (_owner.PersonalityProfile.Caution <= 0.6f || _bot.Memory.HaveEnemy || !_lay.CanProne)
                return;

            Collider[] zones = Physics.OverlapSphere(_bot.Position, TriggerZoneRadius, TriggerLayerMask);
            for (int i = 0; i < zones.Length; i++)
            {
                var zone = zones[i];
                if (zone == null) continue;

                string zoneName = zone.name.ToLowerInvariant();
                if (zoneName.Contains("choke") || zoneName.Contains("ambush"))
                {
                    _lay.TryLay();
                    _mover.SetTargetMoveSpeed(0f);
                    _bot.BotTalk?.TrySay(EPhraseTrigger.Look);
                    return;
                }
            }
        }

        #endregion

        #region Room Searching

        private bool TryRoomSearchFlow()
        {
            var places = _bot.BotsGroup?.PlacesForCheck;
            if (places == null || places.Count == 0)
                return false;

            int offset = (_groupSlotIndex >= 0) ? _groupSlotIndex % places.Count : 0;
            int index = (_currentRoomIndex + offset) % places.Count;
            Vector3 target = places[index].Position;

            if (!HasLineOfSightTo(target))
            {
                StartCoroutine(PeekWithTilt(target));
                return true;
            }

            Vector3 toTarget = target - _bot.Position;
            float sqrDist = toTarget.sqrMagnitude;

            if (sqrDist > 4f)
            {
                float moveSpeed = Mathf.Lerp(0.25f, 0.8f, 1f - _owner.PersonalityProfile.Caution);
                _mover.SetTargetMoveSpeed(moveSpeed);

                if (Physics.Raycast(_bot.Position, toTarget.normalized, out var hit, 2f))
                {
                    if (hit.collider.TryGetComponent(out Door door) && door.DoorState != EDoorState.Open)
                    {
                        StartCoroutine(DoorStackBreach(door, target));
                        return true;
                    }
                }

                BotMovementHelper.SmoothMoveTo(_bot, target);
            }
            else
            {
                _currentRoomIndex = (_currentRoomIndex + 1) % places.Count;
                StartCoroutine(CornerScanPause());
            }

            return true;
        }

        #endregion

        #region Suppression / Ambush Detection

        private bool TrySuppressOrAmbush()
        {
            if (_owner.PersonalityProfile.Caution > 0.75f &&
                _cache.LastHeardTime + 6f > Time.time &&
                !_bot.Memory.HaveEnemy &&
                _lay.CanProne)
            {
                _lay.TryLay();
                _mover.SetTargetMoveSpeed(0f);
                _bot.BotTalk?.TrySay(EPhraseTrigger.OnBeingHurt);
                return true;
            }

            return false;
        }

        #endregion

        #region Tactical Peeking

        private IEnumerator PeekWithTilt(Vector3 target)
        {
            _mover.SetTargetMoveSpeed(0f);

            var direction = Random.value > 0.5f ? BotTiltType.left : BotTiltType.right;
            _bot.Tilt.Set(direction);

            yield return new WaitForSeconds(LeanDuration);

            _bot.BotTalk?.TrySay(EPhraseTrigger.Look);

            _bot.Tilt.Stop();

            yield return new WaitForSeconds(0.2f);
            BotMovementHelper.SmoothMoveTo(_bot, target);
        }

        private bool HasLineOfSightTo(Vector3 target)
        {
            return !Physics.Linecast(_bot.Position + Vector3.up * 1.5f, target, out _, LOSLayerMask);
        }

        #endregion

        #region Door Breaching

        private IEnumerator DoorStackBreach(Door door, Vector3 roomTarget)
        {
            yield return new WaitForSeconds(Random.Range(0.25f, 0.5f));

            int squadCount = 0;
            var members = _bot.BotsGroup?._members;
            if (members != null)
            {
                foreach (var mate in members)
                {
                    if (mate != null && mate != _bot &&
                        (mate.Position - door.transform.position).sqrMagnitude < 9f)
                        squadCount++;
                }
            }

            if (squadCount > 0)
                yield return new WaitForSeconds(DoorStackWait);

            if (door.DoorState != EDoorState.Open)
            {
                door.Interact(new InteractionResult(EInteractionType.Open));
                _bot.BotTalk?.TrySay(EPhraseTrigger.GoForward);
            }

            yield return new WaitForSeconds(0.25f);
            BotMovementHelper.SmoothMoveTo(_bot, roomTarget);
        }

        #endregion

        #region Corner Scan Pause

        private IEnumerator CornerScanPause()
        {
            float wait = Random.Range(0.5f, 1.5f);
            yield return new WaitForSeconds(wait);

            if (Random.value < 0.3f)
                _bot.BotTalk?.TrySay(EPhraseTrigger.Look);
        }

        #endregion
    }
}
