﻿// <auto-generated>
//   This file is part of AI-Refactored, an open-source project focused on realistic AI behavior in Escape from Tarkov.
//   Licensed under the MIT License. See LICENSE in the repository root for more information.
//
//   THIS FILE IS SYSTEMATICALLY MANAGED.
//   All logic is bulletproof and strictly contained—never fallback to vanilla AI, never cascade errors.
//   Realism Pass: Human-like hesitation, micro-randomization, and organic error handling.
// </auto-generated>

namespace AIRefactored.AI.Navigation
{
    using System;
    using AIRefactored.Core;
    using BepInEx.Logging;
    using EFT;
    using EFT.Interactive;
    using UnityEngine;

    /// <summary>
    /// Handles door interactions for bots using bulletproof, fallback-agnostic, deadlock-free logic.
    /// Replaces EFT.BotDoorInteraction with fully AIRefactored logic.
    /// All failures are locally isolated; cannot break or cascade into other systems.
    /// </summary>
    public sealed class BotDoorInteractionSystem
    {
        #region Constants

        private const float DoorRetryCooldown = 2.5f;
        private const float DoorCheckInterval = 0.4f;
        private const float DoorCastRange = 1.75f;
        private const float DoorCastRadius = 0.4f;
        private const float HesitateChance = 0.11f; // 11% chance to hesitate before interacting
        private const float HesitateMinDelay = 0.17f;
        private const float HesitateMaxDelay = 0.52f;

        #endregion

        #region Fields

        private readonly BotOwner _bot;
        private readonly ManualLogSource _log;

        private float _lastDoorCheckTime;
        private float _nextRetryTime;
        private Door _currentDoor;
        private float _hesitateUntil;

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether a door is currently blocking the bot.
        /// </summary>
        public bool IsBlockedByDoor { get; private set; }

        #endregion

        #region Constructor

        /// <summary>
        /// Creates a new BotDoorInteractionSystem with strict null safety and logging.
        /// </summary>
        public BotDoorInteractionSystem(BotOwner bot)
        {
            if (bot == null)
                throw new ArgumentNullException(nameof(bot), "[BotDoorInteractionSystem] Constructor: BotOwner was null.");

            _bot = bot;
            _log = Plugin.LoggerInstance;
            _hesitateUntil = 0f;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Updates door interaction logic. Bulletproof: never breaks or cascades on error.
        /// </summary>
        public void Tick(float time)
        {
            try
            {
                if (_bot == null || _bot.IsDead)
                    return;

                Player player = EFTPlayerUtil.ResolvePlayer(_bot);
                if (!EFTPlayerUtil.IsValid(player) || !player.IsAI || player.CurrentManagedState == null)
                    return;

                if (time < _lastDoorCheckTime + DoorCheckInterval)
                    return;
                _lastDoorCheckTime = time;

                Vector3 origin = _bot.Position + Vector3.up * 1.2f;
                Vector3 forward = _bot.LookDirection;

                RaycastHit hit;
                if (!Physics.SphereCast(origin, DoorCastRadius, forward, out hit, DoorCastRange, AIRefactoredLayerMasks.Interactive))
                {
                    ClearDoorState();
                    return;
                }

                Collider col = hit.collider;
                if (col == null)
                {
                    ClearDoorState();
                    return;
                }

                Door door = col.GetComponentInParent<Door>();
                if (door == null || !door.enabled || !door.Operatable)
                {
                    ClearDoorState();
                    return;
                }

                EDoorState state = door.DoorState;
                if ((state & EDoorState.Open) != 0 || (state & EDoorState.Breaching) != 0)
                {
                    ClearDoorState();
                    return;
                }

                if (state == EDoorState.Interacting)
                {
                    MarkBlocked(door);
                    return;
                }

                if (time < _nextRetryTime)
                {
                    MarkBlocked(door);
                    return;
                }

                // Human-like hesitation: bots sometimes pause before interacting with a door, like a real player double-checking the risk.
                if (_hesitateUntil > time)
                {
                    MarkBlocked(door);
                    return;
                }
                if (UnityEngine.Random.value < HesitateChance)
                {
                    _hesitateUntil = time + UnityEngine.Random.Range(HesitateMinDelay, HesitateMaxDelay);
                    MarkBlocked(door);
                    return;
                }

                try
                {
                    EInteractionType interactionType = GetBestInteractionType(state);
                    InteractionResult result = new InteractionResult(interactionType);
                    player.CurrentManagedState.StartDoorInteraction(door, result, null);

                    _log.LogDebug("[BotDoorInteraction] " + _bot.name + " → " + interactionType + " door " + door.name);
                }
                catch (Exception ex)
                {
                    _log.LogError("[BotDoorInteraction] Door interaction failed: " + ex);
                }

                _nextRetryTime = time + DoorRetryCooldown;
                _currentDoor = door;
                IsBlockedByDoor = true;
            }
            catch
            {
                // Locally isolated; cannot break or cascade.
                ClearDoorState();
            }
        }

        /// <summary>
        /// Checks if a door is currently blocking the specified position.
        /// </summary>
        public bool IsDoorBlocking(Vector3 position)
        {
            try
            {
                if (_currentDoor == null || !_currentDoor.enabled)
                    return false;

                float dist = Vector3.Distance(_currentDoor.transform.position, position);
                return dist < DoorCastRange && (_currentDoor.DoorState & EDoorState.Open) == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Clears door interaction state and resets cooldowns.
        /// </summary>
        public void Reset()
        {
            _currentDoor = null;
            IsBlockedByDoor = false;
            _nextRetryTime = 0f;
            _lastDoorCheckTime = 0f;
            _hesitateUntil = 0f;
        }

        #endregion

        #region Private Helpers

        private void ClearDoorState()
        {
            _currentDoor = null;
            IsBlockedByDoor = false;
            _hesitateUntil = 0f;
        }

        private void MarkBlocked(Door door)
        {
            _currentDoor = door;
            IsBlockedByDoor = true;
        }

        private static EInteractionType GetBestInteractionType(EDoorState state)
        {
            if ((state & EDoorState.Shut) != 0 || state == EDoorState.None)
                return EInteractionType.Open;

            if ((state & EDoorState.Open) != 0)
                return EInteractionType.Close;

            return EInteractionType.Open;
        }

        #endregion
    }
}
