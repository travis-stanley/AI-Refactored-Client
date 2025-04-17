#nullable enable

using UnityEngine;
using EFT;
using AIRefactored.Core;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Extension methods for enhancing bot memory, tactical state transitions, and auditory memory.
    /// </summary>
    public static class BotMemoryExtensions
    {
        #region Tactical Movement

        /// <summary>
        /// Commands the bot to fallback to a safer position using pathfinding.
        /// </summary>
        /// <param name="bot">The bot issuing the fallback.</param>
        /// <param name="fallbackPosition">The position to retreat to.</param>
        public static void FallbackTo(this BotOwner bot, Vector3 fallbackPosition)
        {
            if (bot == null || bot.IsDead || fallbackPosition == Vector3.zero)
                return;

            bot.GoToPoint(fallbackPosition, slowAtTheEnd: true);
        }

        /// <summary>
        /// Forces the bot to immediately move to a target position.
        /// </summary>
        /// <param name="bot">The bot to move.</param>
        /// <param name="position">Target position.</param>
        public static void ForceMoveTo(this BotOwner bot, Vector3 position)
        {
            if (bot == null || bot.IsDead)
                return;

            bot.GoToPoint(position, slowAtTheEnd: true);
        }

        /// <summary>
        /// Placeholder for future dynamic cover reassessment logic.
        /// </summary>
        /// <param name="bot">The bot to update cover from.</param>
        public static void ReevaluateCurrentCover(this BotOwner bot)
        {
            if (bot == null || bot.IsDead)
                return;

            // TODO: Implement dynamic cover lookup and repositioning.
        }

        #endregion

        #region Behavior Mode Flags

        /// <summary>
        /// Flags the bot to act cautiously — used in stealth or after hearing threats.
        /// </summary>
        public static void SetCautiousSearchMode(this BotOwner bot)
        {
            if (bot == null || bot.IsDead)
                return;

            bot.Memory.AttackImmediately = false;
            bot.Memory.IsPeace = false;
        }

        /// <summary>
        /// Enables aggressive combat behavior.
        /// </summary>
        public static void SetCombatAggressionMode(this BotOwner bot)
        {
            if (bot == null || bot.IsDead)
                return;

            bot.Memory.AttackImmediately = true;
            bot.Memory.IsPeace = false;
        }

        /// <summary>
        /// Switches the bot into patrol/peaceful behavior logic.
        /// </summary>
        public static void SetPeaceMode(this BotOwner bot)
        {
            if (bot == null || bot.IsDead)
                return;

            bot.Memory.AttackImmediately = false;
            bot.Memory.IsPeace = true;
            bot.Memory.CheckIsPeace();
        }

        #endregion

        #region Auditory Memory

        /// <summary>
        /// Logs a recent sound heard by the bot and moves it cautiously toward the source.
        /// </summary>
        /// <param name="bot">The bot who heard the sound.</param>
        /// <param name="source">The player who made the sound.</param>
        public static void SetLastHeardSound(this BotOwner bot, Player source)
        {
            if (bot == null || bot.IsDead || source == null || bot.ProfileId == source.ProfileId)
                return;

            // Register sound with EFT group controller (optional)
            bot.BotsGroup?.LastSoundsController?.AddNeutralSound(source, source.Position);

            // Register in AIRefactored memory
            BotMemoryStore.AddHeardSound(bot.ProfileId, source.Position, Time.time);

            // Advance cautiously toward source
            Vector3 cautiousAdvance = source.Position + (bot.Position - source.Position).normalized * 3f;
            bot.GoToPoint(cautiousAdvance, slowAtTheEnd: false);

            // Optional: voice line trigger
            bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyShot);
        }

        /// <summary>
        /// Clears any previously stored auditory memory data for the bot.
        /// </summary>
        public static void ClearLastHeardSound(this BotOwner bot)
        {
            if (bot == null || bot.IsDead)
                return;

            BotMemoryStore.ClearHeardSound(bot.ProfileId);
        }

        #endregion
    }
}
