#nullable enable

using AIRefactored.AI.Behavior;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Components;
using AIRefactored.AI.Groups;
using AIRefactored.AI.Movement;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Reactions;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Core
{
    /// <summary>
    /// Caches all AIRefactored-related components and helpers for a bot.
    /// Used to unify AI behaviors, perception, and tactical modules into a single access point.
    /// </summary>
    public class BotComponentCache : MonoBehaviour
    {
        #region Core References

        public BotOwner? Bot { get; internal set; }

        public FlashGrenadeComponent? FlashGrenade { get; private set; }
        public BotPanicHandler? PanicHandler { get; private set; }
        public BotSuppressionReactionComponent? Suppression { get; private set; }

        public AIRefactoredBotOwner? AIRefactoredBotOwner { get; private set; }
        public BotBehaviorEnhancer? BehaviorEnhancer { get; private set; }
        public BotGroupBehavior? GroupBehavior { get; private set; }

        public BotMovementController? Movement { get; private set; }
        public BotTacticalDeviceController? Tactical { get; private set; }

        public HearingDamageComponent? HearingDamage { get; private set; }

        public BotOwnerPathfindingCache? PathCache { get; private set; }

        public BotTilt? Tilt { get; private set; } // ✅ BotTilt now cached here

        #endregion

        #region Perception State

        public bool IsBlinded { get; set; } = false;
        public float BlindUntilTime { get; set; } = 0f;
        public float LastFlashTime { get; set; } = 0f;

        #endregion

        #region Hearing Tracking

        public float LastHeardTime { get; private set; } = -999f;
        public Vector3? LastHeardDirection { get; private set; }

        public void RegisterHeardSound(Vector3 source)
        {
            if (Bot?.GetPlayer == null || !Bot.GetPlayer.IsAI)
                return;

            LastHeardTime = Time.time;
            LastHeardDirection = source - Bot.Position;
        }

        #endregion

        #region Properties

        public bool IsReady =>
            Bot != null &&
            FlashGrenade != null &&
            PanicHandler != null &&
            Suppression != null &&
            Movement != null &&
            Tactical != null;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            Bot = GetComponent<BotOwner>();
            if (Bot == null)
            {
                Debug.LogError("[AIRefactored-Cache] ❌ BotComponentCache missing BotOwner!");
                return;
            }

            FlashGrenade = GetComponent<FlashGrenadeComponent>();
            PanicHandler = GetComponent<BotPanicHandler>();
            Suppression = GetComponent<BotSuppressionReactionComponent>();
            AIRefactoredBotOwner = GetComponent<AIRefactoredBotOwner>();
            BehaviorEnhancer = GetComponent<BotBehaviorEnhancer>();
            GroupBehavior = GetComponent<BotGroupBehavior>();
            Movement = GetComponent<BotMovementController>();
            Tactical = GetComponent<BotTacticalDeviceController>();
            HearingDamage = GetComponent<HearingDamageComponent>();

            PathCache = new BotOwnerPathfindingCache();

            Tilt = new BotTilt(Bot); 
        }

        #endregion

        #region Reset Support

        public void Reset()
        {
            IsBlinded = false;
            BlindUntilTime = 0f;
            LastFlashTime = 0f;
            LastHeardTime = -999f;
            LastHeardDirection = null;

            PathCache?.Clear();
        }

        #endregion
    }
}
