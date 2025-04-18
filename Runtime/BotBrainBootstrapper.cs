#nullable enable

using AIRefactored.AI.Threads;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Lightweight hook that wires BotBrain only onto valid AI bots at runtime.
    /// </summary>
    public class BotBrainBootstrapper : MonoBehaviour
    {
        private void Start()
        {
            if (Singleton<BotSpawner>.Instantiated)
            {
                Singleton<BotSpawner>.Instance.OnBotCreated += HandleBotCreated;
                Debug.Log("[AIRefactored] ✅ BotBrainBootstrapper active.");
            }
            else
            {
                Debug.LogError("[AIRefactored] ❌ BotSpawner unavailable — cannot hook AI brains.");
            }
        }

        private void OnDestroy()
        {
            if (Singleton<BotSpawner>.Instantiated)
                Singleton<BotSpawner>.Instance.OnBotCreated -= HandleBotCreated;
        }

        private void HandleBotCreated(BotOwner bot)
        {
            if (!IsEligible(bot))
                return;

            var obj = bot.GetPlayer?.gameObject;
            if (obj == null || obj.GetComponent<BotBrain>() != null)
                return;

            var brain = obj.AddComponent<BotBrain>();
            brain.Initialize(bot);

            Debug.Log($"[AIRefactored] 🤖 BotBrain attached to {bot.Profile?.Info?.Nickname ?? "unknown"}");
        }

        private static bool IsEligible(BotOwner bot)
        {
            var player = bot.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }
    }
}
