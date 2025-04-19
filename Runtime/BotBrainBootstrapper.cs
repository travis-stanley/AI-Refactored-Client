#nullable enable

using AIRefactored.AI.Threads;
using Comfort.Common;
using EFT;
using UnityEngine;

namespace AIRefactored.Runtime
{
    /// <summary>
    /// Lightweight runtime hook that attaches BotBrain to valid AI bots when spawned.
    /// Ensures only real AI-controlled bots receive our unified AI controller.
    /// </summary>
    public class BotBrainBootstrapper : MonoBehaviour
    {
        #region Unity Lifecycle

        private void Start()
        {
            if (Singleton<BotSpawner>.Instantiated)
            {
                Singleton<BotSpawner>.Instance.OnBotCreated += HandleBotCreated;
                Debug.Log("[AIRefactored] ✅ BotBrainBootstrapper active and listening for bot spawns.");
            }
            else
            {
                Debug.LogError("[AIRefactored] ❌ BotSpawner unavailable — cannot attach AI brains.");
            }
        }

        private void OnDestroy()
        {
            if (Singleton<BotSpawner>.Instantiated)
                Singleton<BotSpawner>.Instance.OnBotCreated -= HandleBotCreated;
        }

        #endregion

        #region Core Logic

        private void HandleBotCreated(BotOwner bot)
        {
            if (!IsEligible(bot))
                return;

            GameObject? playerObject = bot.GetPlayer?.gameObject;
            if (playerObject == null || playerObject.GetComponent<BotBrain>() != null)
                return;

            var brain = playerObject.AddComponent<BotBrain>();
            brain.Initialize(bot);

            Debug.Log($"[AIRefactored] 🤖 BotBrain attached to {bot.Profile?.Info?.Nickname ?? "unknown"}");
        }

        private static bool IsEligible(BotOwner bot)
        {
            var player = bot.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        #endregion
    }
}
