#nullable enable

using UnityEngine;
using EFT;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Provides high-level movement and tactical behavior flags for BotOwner agents.
    /// These simulate realistic fallback, aggressive, or cautious tactical transitions.
    /// </summary>
    public static class BotMemoryExtensions
    {
        /// <summary>
        /// Simulates a fallback movement by commanding the bot to move to a new position.
        /// </summary>
        public static void FallbackTo(this BotOwner bot, Vector3 fallbackPosition)
        {
            if (bot == null || fallbackPosition == Vector3.zero)
                return;

            bot.GoToPoint(fallbackPosition, true);

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored] Bot {bot.Profile?.Info?.Nickname} performing fallback to {fallbackPosition}");
#endif
        }

        /// <summary>
        /// Forces the bot to move immediately to a position, regardless of combat state.
        /// </summary>
        public static void ForceMoveTo(this BotOwner bot, Vector3 position)
        {
            if (bot == null)
                return;

            bot.GoToPoint(position, true);

#if UNITY_EDITOR
            Debug.Log($"[AIRefactored] Bot {bot.Profile?.Info?.Nickname} forced move to {position}");
#endif
        }

        /// <summary>
        /// Triggers reevaluation of cover behavior. Placeholder for more advanced threat/position logic.
        /// </summary>
        public static void ReevaluateCurrentCover(this BotOwner bot)
        {
#if UNITY_EDITOR
            Debug.Log($"[AIRefactored] Bot {bot.Profile?.Info?.Nickname} reevaluating cover behavior");
#endif
        }

        /// <summary>
        /// Flags the bot to behave with caution — potentially used for group or patrol AI.
        /// </summary>
        public static void SetCautiousSearchMode(this BotOwner bot)
        {
#if UNITY_EDITOR
            Debug.Log($"[AIRefactored] Bot {bot.Profile?.Info?.Nickname} using cautious search mode");
#endif
        }

        /// <summary>
        /// Flags the bot to enter an aggressive mode — can influence VO, group behavior, or movement.
        /// </summary>
        public static void SetCombatAggressionMode(this BotOwner bot)
        {
#if UNITY_EDITOR
            Debug.Log($"[AIRefactored] Bot {bot.Profile?.Info?.Nickname} using combat aggression mode");
#endif
        }
    }
}
