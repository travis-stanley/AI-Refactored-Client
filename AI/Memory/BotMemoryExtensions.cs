#nullable enable

using AIRefactored.AI.Helpers;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Extension methods for enhancing bot memory, tactical transitions, auditory reactions, and search behaviors.
    /// </summary>
    public static class BotMemoryExtensions
    {
        #region Tactical Movement

        /// <summary>
        /// Moves the bot to a fallback position if not panicking or invalid.
        /// </summary>
        public static void FallbackTo(this BotOwner bot, Vector3 fallbackPosition)
        {
            if (!IsValid(bot) || fallbackPosition.sqrMagnitude < 0.5f)
                return;

            var cache = BotCacheUtility.GetCache(bot);
            if (cache?.PanicHandler?.IsPanicking == true)
                return;

            BotMovementHelper.SmoothMoveTo(bot, fallbackPosition, true, 1f);
        }

        /// <summary>
        /// Forces the bot to move to the target position regardless of panic or other states.
        /// </summary>
        public static void ForceMoveTo(this BotOwner bot, Vector3 position)
        {
            if (!IsValid(bot) || position.sqrMagnitude < 0.5f)
                return;

            BotMovementHelper.SmoothMoveTo(bot, position, true, 1f);
        }

        /// <summary>
        /// If current cover is exposed and the enemy is visible, the bot will reposition.
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

            if (angle < 20f && toEnemy.sqrMagnitude < 625f)
            {
                Vector3 fallback = bot.Position - toEnemy.normalized * 5f;
                BotMovementHelper.SmoothMoveTo(bot, fallback, true, 1f);
                bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);
            }
        }

        #endregion

        #region Behavior Mode Flags

        /// <summary>
        /// Puts the bot in a cautious search state without immediate attack.
        /// </summary>
        public static void SetCautiousSearchMode(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory == null)
                return;

            bot.Memory.AttackImmediately = false;
            bot.Memory.IsPeace = false;
        }

        /// <summary>
        /// Enables aggressive combat mode where the bot will attack without hesitation.
        /// </summary>
        public static void SetCombatAggressionMode(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory == null)
                return;

            bot.Memory.AttackImmediately = true;
            bot.Memory.IsPeace = false;
        }

        /// <summary>
        /// Sets the bot into peaceful mode (non-combat state).
        /// </summary>
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

        /// <summary>
        /// Records the last sound the bot heard and moves cautiously toward the sound source.
        /// </summary>
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

        /// <summary>
        /// Clears any auditory memory the bot is tracking.
        /// </summary>
        public static void ClearLastHeardSound(this BotOwner bot)
        {
            if (!IsValid(bot))
                return;

            BotMemoryStore.ClearHeardSound(bot.ProfileId);
        }

        #endregion

        #region Tactical Evaluation

        /// <summary>
        /// Attempts to get a general direction from which the bot is being flanked.
        /// </summary>
        public static Vector3? TryGetFlankDirection(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory?.GoalEnemy == null)
                return null;

            Vector3 toEnemy = bot.Memory.GoalEnemy.CurrPosition - bot.Position;
            return Vector3.Cross(toEnemy.normalized, Vector3.up);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Ensures the bot is valid, not dead, and under AI control.
        /// </summary>
        private static bool IsValid(BotOwner? bot)
        {
            return bot != null && bot.GetPlayer != null && bot.GetPlayer.IsAI && !bot.IsDead;
        }

        #endregion
    }
}
