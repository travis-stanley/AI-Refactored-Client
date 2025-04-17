#nullable enable

using System.Collections.Generic;
using Comfort.Common;
using EFT;
using EFT.Bots;
using UnityEngine;
using AIRefactored.AI.Core;
using AIRefactored.AI.Optimization;
using AIRefactored.AI.Combat;
using AIRefactored.AI.Reactions;
using AIRefactored.AI.Behavior;
using AIRefactored.AI.Missions;
using AIRefactored.AI.Perception;
using AIRefactored.AI.Memory;
using AIRefactored.AI.Groups;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Automatically wires all AIRefactored behavior systems into newly spawned bots.
    /// Ensures caching, memory, perception, and group sync logic is attached on spawn.
    /// </summary>
    public class BotInitializer : MonoBehaviour
    {
        #region Fields

        private static readonly HashSet<BotOwner> _processed = new();
        private static readonly BotOwnerStateCache _stateCache = new();
        private readonly BotOwnerGroupOptimization _groupOptimizer = new();

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            if (Singleton<BotSpawner>.Instance != null)
            {
                Singleton<BotSpawner>.Instance.OnBotCreated += OnBotSpawned;
                Debug.Log("[AIRefactored] ✅ Hooked into BotSpawner.OnBotCreated.");
            }
            else
            {
                Debug.LogWarning("[AIRefactored] ❌ BotSpawner not available. Falling back to polling.");
            }

            InvokeRepeating(nameof(ScanForUnpatchedBots), 2f, 3f);
        }

        private void OnDestroy()
        {
            if (Singleton<BotSpawner>.Instantiated)
                Singleton<BotSpawner>.Instance.OnBotCreated -= OnBotSpawned;
        }

        #endregion

        #region Bot Wiring

        /// <summary>
        /// Called when a new bot is created. Wires AI systems.
        /// </summary>
        private void OnBotSpawned(BotOwner bot)
        {
            if (bot == null || bot.GetPlayer == null || _processed.Contains(bot))
                return;

            AttachBrain(bot);
        }

        /// <summary>
        /// Fallback polling method in case bot spawn events were missed.
        /// </summary>
        private void ScanForUnpatchedBots()
        {
            foreach (var bot in GameObject.FindObjectsOfType<BotOwner>())
            {
                if (!_processed.Contains(bot) && bot.GetPlayer != null)
                    AttachBrain(bot);
            }
        }

        /// <summary>
        /// Attaches all required AIRefactored systems to a bot and initializes caches and group sync.
        /// </summary>
        private void AttachBrain(BotOwner bot)
        {
            var obj = bot.GetPlayer?.gameObject;
            if (obj == null)
                return;

            // === Core Cache ===
            var cache = bot.gameObject.GetComponent<BotComponentCache>() ?? bot.gameObject.AddComponent<BotComponentCache>();
            cache.Bot = bot;

            // === AI Subsystems ===
            obj.TryAddComponent<BotAIController>();
            obj.TryAddComponent<BotBehaviorEnhancer>()?.Init(bot);
            obj.TryAddComponent<BotMissionSystem>()?.Init(bot);
            obj.TryAddComponent<BotTacticalDeviceController>();
            obj.TryAddComponent<CombatStateMachine>();
            obj.TryAddComponent<BotSuppressionReactionComponent>();
            obj.TryAddComponent<BotThreatEscalationMonitor>();
            obj.TryAddComponent<BotGroupSyncCoordinator>()?.Init(bot);
            obj.TryAddComponent<BotPanicHandler>();
            obj.TryAddComponent<FlashGrenadeComponent>();
            obj.TryAddComponent<BotFlashReactionComponent>();
            obj.TryAddComponent<BotVisionSystem>();
            obj.TryAddComponent<BotHearingSystem>();
            obj.TryAddComponent<BotAsyncProcessor>()?.Initialize(bot);

            // === Register State Snapshot for Performance
            _stateCache.CacheBotOwnerState(bot);

            _processed.Add(bot);
            TryOptimizeGroup(bot);

            Debug.Log($"[AIRefactored] 🤖 Bot initialized: {bot.Profile?.Info?.Nickname ?? "???"}");
        }

        /// <summary>
        /// Applies group-level optimization tuning for squad behavior.
        /// </summary>
        private void TryOptimizeGroup(BotOwner bot)
        {
            var group = bot.BotsGroup;
            if (group == null || group.MembersCount <= 1)
                return;

            var bots = new List<BotOwner>();
            for (int i = 0; i < group.MembersCount; i++)
            {
                var member = group.Member(i);
                if (member != null)
                    bots.Add(member);
            }

            _groupOptimizer.OptimizeGroupAI(bots);
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for safe component binding on bot objects.
    /// </summary>
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Adds the component to the GameObject if it does not already exist.
        /// </summary>
        /// <typeparam name="T">The component type to add.</typeparam>
        /// <param name="obj">Target GameObject.</param>
        /// <returns>Existing or newly added component.</returns>
        public static T? TryAddComponent<T>(this GameObject obj) where T : Component
        {
            return obj.GetComponent<T>() ?? obj.AddComponent<T>();
        }
    }
}
