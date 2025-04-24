#nullable enable

using AIRefactored.Runtime;
using UnityEngine;

namespace AIRefactored.AI.Threads
{
    /// <summary>
    /// Ensures only AIRefactored.BotBrain is active on AI-controlled bots.
    /// Scans the bot GameObject and removes conflicting brain MonoBehaviours injected by other mods.
    /// </summary>
    public static class BotBrainGuardian
    {
        #region Public API

        /// <summary>
        /// Scans a bot GameObject for conflicting or injected MonoBehaviours.
        /// Removes AI logic from other mods (e.g. SAIN, SPT, LuaBrains).
        /// </summary>
        /// <param name="botGameObject">The bot GameObject to sanitize.</param>
        public static void Enforce(GameObject botGameObject)
        {
            if (botGameObject == null)
                return;

            MonoBehaviour[] components = botGameObject.GetComponents<MonoBehaviour>();
            if (components == null || components.Length == 0)
                return;

            for (int i = 0; i < components.Length; i++)
            {
                MonoBehaviour? comp = components[i];
                if (comp == null)
                    continue;

                var type = comp.GetType();
                string ns = type.Namespace?.ToLowerInvariant() ?? string.Empty;
                string name = type.Name.ToLowerInvariant();

                // Preserve AIRefactored.BotBrain
                if (type == typeof(BotBrain))
                    continue;

                // Preserve Unity, EFT, Comfort, or clearly safe systems
                if (ns.StartsWith("unity") || ns.StartsWith("eft") || ns.Contains("comfort"))
                    continue;

                if (IsConflictingBrain(type, name, ns))
                {
                    // Destroy safely
                    if (comp != null)
                    {
                        Object.Destroy(comp);
                        AIRefactoredController.Logger.LogWarning(
                            $"[BotBrainGuardian] ⚠ Removed conflicting AI component '{type.FullName}' from GameObject '{botGameObject.name}'"
                        );
                    }
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Determines whether the component appears to be a conflicting AI brain or logic controller.
        /// </summary>
        private static bool IsConflictingBrain(System.Type type, string name, string ns)
        {
            return name.Contains("brain")
                || name.StartsWith("pmc")
                || name.StartsWith("boss")
                || name.StartsWith("follower")
                || name.StartsWith("assault")
                || name.StartsWith("exusec")
                || ns.Contains("spt")
                || ns.Contains("sain")
                || ns.Contains("lua");
        }

        #endregion
    }
}
