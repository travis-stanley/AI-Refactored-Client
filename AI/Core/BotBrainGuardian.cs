#nullable enable

using AIRefactored.Runtime;
using UnityEngine;

namespace AIRefactored.AI.Threads
{
    /// <summary>
    /// Protects AI bots from conflicting brain components injected by SPT or other mods.
    /// Ensures only AIRefactored.BotBrain is allowed to control AI logic.
    /// </summary>
    public static class BotBrainGuardian
    {
        /// <summary>
        /// Called after our BotBrain is added to a bot. Removes all other brain components aggressively.
        /// </summary>
        public static void Enforce(GameObject botGameObject)
        {
            if (botGameObject == null)
                return;

            MonoBehaviour[] components = botGameObject.GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp == null)
                    continue;

                var type = comp.GetType();

                // === Safe if it IS our brain ===
                if (type == typeof(BotBrain))
                    continue;

                // === Safe if from Unity or EFT core ===
                string ns = type.Namespace?.ToLowerInvariant() ?? "";
                if (ns.StartsWith("unity") || ns.StartsWith("eft") || ns.Contains("comfort"))
                    continue;

                // === Check for 'brain-like' names (aggressively match known SPT patterns) ===
                string name = type.Name.ToLowerInvariant();
                bool isInjectedSPTBrain =
                    name.Contains("brain") ||
                    name.StartsWith("pmc") ||
                    name.StartsWith("boss") ||
                    name.StartsWith("follower") ||
                    name.StartsWith("assault") ||
                    name.StartsWith("exusec") ||
                    ns.Contains("spt") ||
                    ns.Contains("sain") ||
                    ns.Contains("lua");

                if (isInjectedSPTBrain)
                {
                    Object.Destroy(comp);
                    AIRefactoredController.Logger.LogWarning($"[BotBrainGuardian] ⚠ Removed injected AI logic: {type.FullName}");
                }
            }
        }
    }
}
