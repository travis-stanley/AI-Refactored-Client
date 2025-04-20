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
            StartCoroutine(DelayedHook());
        }

        private System.Collections.IEnumerator DelayedHook()
        {
            yield return new WaitUntil(() => Singleton<BotSpawner>.Instantiated);

            Singleton<BotSpawner>.Instance.OnBotCreated += HandleBotCreated;
            Plugin.LoggerInstance.LogInfo("[AIRefactored] ✅ BotBrainBootstrapper active and listening for bot spawns.");
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
            {
                Plugin.LoggerInstance.LogDebug($"[AIRefactored] Skipped bot: {bot.Profile?.Id ?? "unknown"} (not AI or local player)");
                return;
            }

            GameObject? playerObject = bot.GetPlayer?.gameObject;
            if (playerObject == null || playerObject.GetComponent<BotBrain>() != null)
                return;

            var brain = playerObject.AddComponent<BotBrain>();
            brain.Initialize(bot);

            string name = bot.Profile?.Info?.Nickname ?? "UnnamedBot";
            Plugin.LoggerInstance.LogInfo($"[AIRefactored] 🤖 BotBrain attached to {name}");
        }

        private static bool IsEligible(BotOwner bot)
        {
            var player = bot.GetPlayer;
            return player != null && player.IsAI && !player.IsYourPlayer;
        }

        #endregion
    }
}
