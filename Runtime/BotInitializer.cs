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

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Listens for bot spawns and automatically attaches AIRefactored logic.
    /// Ensures all behavioral, perception, and optimization systems are wired in.
    /// </summary>
    public class BotInitializer : MonoBehaviour
    {
        #region Fields

        private static readonly HashSet<BotOwner> _processed = new();

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
                Debug.LogWarning("[AIRefactored] ❌ BotSpawner not found. Fallback to polling.");
            }

            InvokeRepeating(nameof(ScanForUnpatchedBots), 2f, 3f);
        }

        private void OnDestroy()
        {
            if (Singleton<BotSpawner>.Instantiated)
            {
                Singleton<BotSpawner>.Instance.OnBotCreated -= OnBotSpawned;
            }
        }

        #endregion

        #region Bot Attachment Logic

        private void OnBotSpawned(BotOwner bot)
        {
            if (bot == null || bot.GetPlayer == null || _processed.Contains(bot))
                return;

            AttachBrain(bot);
        }

        private void ScanForUnpatchedBots()
        {
            foreach (var bot in GameObject.FindObjectsOfType<BotOwner>())
            {
                if (!_processed.Contains(bot) && bot.GetPlayer != null)
                {
                    AttachBrain(bot);
                }
            }
        }

        /// <summary>
        /// Attaches all AIRefactored systems to a bot on spawn.
        /// </summary>
        private void AttachBrain(BotOwner bot)
        {
            var obj = bot.GetPlayer?.gameObject;
            if (obj == null)
                return;

            var cache = bot.gameObject.GetComponent<BotComponentCache>() ?? bot.gameObject.AddComponent<BotComponentCache>();
            cache.Bot = bot;

            obj.TryAddComponent<BotAIController>();
            obj.TryAddComponent<BotBehaviorEnhancer>()?.Init(bot);
            obj.TryAddComponent<BotMissionSystem>()?.Init(bot);
            obj.TryAddComponent<BotTacticalDeviceController>();
            obj.TryAddComponent<FlashGrenadeComponent>();
            obj.TryAddComponent<BotFlashReactionComponent>();
            obj.TryAddComponent<BotPanicHandler>();
            obj.TryAddComponent<BotVisionSystem>();
            obj.TryAddComponent<BotHearingSystem>();
            obj.TryAddComponent<BotOwnerZone>();
            obj.TryAddComponent<BotAsyncProcessor>()?.Initialize(bot);

            _processed.Add(bot);
            Debug.Log($"[AIRefactored] 🤖 Bot initialized: {bot.Profile?.Info?.Nickname ?? "???"}");
        }

        #endregion
    }

    public static class GameObjectExtensions
    {
        public static T? TryAddComponent<T>(this GameObject obj) where T : Component
        {
            return obj.GetComponent<T>() ?? obj.AddComponent<T>();
        }
    }
}
