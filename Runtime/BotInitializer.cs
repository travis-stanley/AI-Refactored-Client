#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Threads;
using Comfort.Common;
using EFT;
using System.Collections.Generic;
using UnityEngine;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Minimal lifecycle hook that attaches BotBrain to each bot on spawn.
    /// All logic is handled inside BotBrain from now on.
    /// </summary>
    public class BotInitializer : MonoBehaviour
    {
        private static readonly HashSet<BotOwner> _processed = new();

        private void Start()
        {
            if (Singleton<BotSpawner>.Instance != null)
            {
                Singleton<BotSpawner>.Instance.OnBotCreated += OnBotSpawned;
                Debug.Log("[AIRefactored] ✅ Hooked into BotSpawner.OnBotCreated.");
            }
            else
            {
                Debug.LogWarning("[AIRefactored] ❌ BotSpawner not found. Falling back to polling.");
            }

            InvokeRepeating(nameof(ScanForUnpatchedBots), 2f, 3f);
        }

        private void OnDestroy()
        {
            if (Singleton<BotSpawner>.Instantiated)
                Singleton<BotSpawner>.Instance.OnBotCreated -= OnBotSpawned;
        }

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
                    AttachBrain(bot);
            }
        }

        private void AttachBrain(BotOwner bot)
        {
            GameObject obj = bot.GetPlayer!.gameObject;
            if (obj.GetComponent<BotBrain>() != null)
                return;

            obj.AddComponent<BotBrain>().Initialize(bot);
            _processed.Add(bot);

            Debug.Log($"[AIRefactored] 🤖 BotBrain initialized → {bot.Profile?.Info?.Nickname ?? "Unnamed"}");
        }
    }

    public static class GameObjectExtensions
    {
        public static T? TryAddComponent<T>(this GameObject obj) where T : Component
        {
            return obj.GetComponent<T>() ?? obj.AddComponent<T>();
        }
    }
}
