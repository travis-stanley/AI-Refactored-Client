#nullable enable

using AIRefactored.Runtime;
using UnityEngine;

namespace AIRefactored.AI.Threads
{
    /// <summary>
    /// Prevents conflicting brain components from interfering with AIRefactored bots.
    /// Ensures only AIRefactored.BotBrain is active by removing other injected MonoBehaviours.
    /// </summary>
    public static class BotBrainGuardian
    {
        /// <summary>
        /// Scans the bot GameObject for foreign MonoBehaviours and removes any components
        /// that are not part of AIRefactored or the Unity/EFT core libraries.
        /// </summary>
        /// <param name="botGameObject">The bot GameObject to sanitize.</param>
        public static void Enforce(GameObject botGameObject)
        {
            if (botGameObject == null)
                return;

            MonoBehaviour[] components = botGameObject.GetComponents<MonoBehaviour>();

            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour? comp = components[i];
                if (comp == null)
                    continue;

                System.Type type = comp.GetType();

                // === Skip our official brain component
                if (type == typeof(BotBrain))
                    continue;

                // === Skip anything from Unity, EFT, or Comfort.Common
                string ns = type.Namespace?.ToLowerInvariant() ?? string.Empty;
                if (ns.StartsWith("unity") || ns.StartsWith("eft") || ns.Contains("comfort"))
                    continue;

                // === Aggressive pattern matching for known injected brains (SPT, SAIN, etc.)
                string name = type.Name.ToLowerInvariant();
                bool isInjectedBrain =
                    name.Contains("brain") ||
                    name.StartsWith("pmc") ||
                    name.StartsWith("boss") ||
                    name.StartsWith("follower") ||
                    name.StartsWith("assault") ||
                    name.StartsWith("exusec") ||
                    ns.Contains("spt") ||
                    ns.Contains("sain") ||
                    ns.Contains("lua");

                if (isInjectedBrain)
                {
                    Object.Destroy(comp);
                    AIRefactoredController.Logger.LogWarning($"[BotBrainGuardian] ⚠ Removed injected AI logic: {type.FullName}");
                }
            }
        }
    }
}
