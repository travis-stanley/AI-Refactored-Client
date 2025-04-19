#nullable enable

using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Extension methods for enhancing bot memory, tactical state transitions, and auditory memory.
    /// </summary>
    public static class BotMemoryExtensions
    {
        #region Tactical Movement

        public static void FallbackTo(this BotOwner bot, Vector3 fallbackPosition)
        {
            if (!IsValid(bot) || fallbackPosition == Vector3.zero)
                return;

            BotMovementHelper.SmoothMoveTo(bot, fallbackPosition, true, 1f);
        }

        public static void ForceMoveTo(this BotOwner bot, Vector3 position)
        {
            if (!IsValid(bot))
                return;

            BotMovementHelper.SmoothMoveTo(bot, position, true, 1f);
        }

        /// <summary>
        /// Dynamically re-checks whether current cover is adequate and moves if exposed.
        /// </summary>
        public static void ReevaluateCurrentCover(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory?.GoalEnemy == null)
                return;

            var enemy = bot.Memory.GoalEnemy;
            if (!enemy.IsVisible)
                return;

            Vector3 toEnemy = enemy.CurrPosition - bot.Position;
            float angle = Vector3.Angle(bot.LookDirection, toEnemy);

            if (angle < 20f && toEnemy.sqrMagnitude < 625f) // within 25m
            {
                Vector3 fallback = bot.Position - toEnemy.normalized * 5f;
                BotMovementHelper.SmoothMoveTo(bot, fallback, true, 1f);
                bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);
            }
        }

        #endregion

        #region Behavior Mode Flags

        public static void SetCautiousSearchMode(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory == null)
                return;

            bot.Memory.AttackImmediately = false;
            bot.Memory.IsPeace = false;
        }

        public static void SetCombatAggressionMode(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory == null)
                return;

            bot.Memory.AttackImmediately = true;
            bot.Memory.IsPeace = false;
        }

        public static void SetPeaceMode(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory == null)
                return;

            bot.Memory.AttackImmediately = false;
            bot.Memory.IsPeace = true;
            bot.Memory.CheckIsPeace();
        }

        #endregion

        #region Auditory Memory

        public static void SetLastHeardSound(this BotOwner bot, Player source)
        {
            if (!IsValid(bot) || source == null || source.ProfileId == bot.ProfileId)
                return;

            bot.BotsGroup?.LastSoundsController?.AddNeutralSound(source, source.Position);
            BotMemoryStore.AddHeardSound(bot.ProfileId, source.Position, Time.time);

            Vector3 cautiousAdvance = source.Position + (bot.Position - source.Position).normalized * 3f;
            BotMovementHelper.SmoothMoveTo(bot, cautiousAdvance, false, 1f);

            bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyShot);
        }

        public static void ClearLastHeardSound(this BotOwner bot)
        {
            if (!IsValid(bot))
                return;

            BotMemoryStore.ClearHeardSound(bot.ProfileId);
        }

        #endregion

        #region Tactical Evaluation

        /// <summary>
        /// Estimates a direction from which the bot is being flanked.
        /// </summary>
        public static Vector3? TryGetFlankDirection(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory?.GoalEnemy == null)
                return null;

            Vector3 toEnemy = bot.Memory.GoalEnemy.CurrPosition - bot.Position;
            return Vector3.Cross(toEnemy.normalized, Vector3.up); // Approximate side/flank vector
        }

        #endregion

        #region Helpers

        private static bool IsValid(BotOwner? bot)
        {
            return bot != null && !bot.IsDead;
        }

        #endregion
    }
}
