#nullable enable

using AIRefactored.AI.Core;
using AIRefactored.AI.Helpers;
using AIRefactored.AI.Optimization;
using EFT;
using UnityEngine;

namespace AIRefactored.AI.Memory
{
    /// <summary>
    /// Tactical extensions for BotOwner memory and behavior.
    /// Provides smart fallback, perception updates, state toggles, and auditory reactions.
    /// </summary>
    public static class BotMemoryExtensions
    {
        #region Tactical Movement

        /// <summary>
        /// Sends the bot to a fallback position if valid and not currently panicking.
        /// </summary>
        /// <param name="bot">The bot requesting fallback movement.</param>
        /// <param name="fallbackPosition">The target fallback position.</param>
        public static void FallbackTo(this BotOwner bot, Vector3 fallbackPosition)
        {
            if (bot == null || fallbackPosition.sqrMagnitude < 0.5f)
                return;

            BotComponentCache? cache = BotCacheUtility.GetCache(bot);
            if (cache == null)
                return;

            var panicHandler = cache.PanicHandler;
            if (panicHandler != null && panicHandler.IsPanicking)
                return;

            BotMovementHelper.SmoothMoveTo(bot, fallbackPosition);
        }


        public static void ForceMoveTo(this BotOwner bot, Vector3 position)
        {
            if (!IsValid(bot) || position.sqrMagnitude < 0.5f)
                return;

            BotMovementHelper.SmoothMoveTo(bot, position);
        }

        public static void ReevaluateCurrentCover(this BotOwner bot)
        {
            if (!IsValid(bot))
                return;

            if (bot.Memory == null || bot.Memory.GoalEnemy == null || !bot.Memory.GoalEnemy.IsVisible)
                return;

            Vector3 toEnemy = bot.Memory.GoalEnemy.CurrPosition - bot.Position;
            if (toEnemy.sqrMagnitude < 1f)
                return;

            float angle = Vector3.Angle(bot.LookDirection, toEnemy.normalized);
            if (angle < 20f && toEnemy.sqrMagnitude < 625f)
            {
                Vector3 fallback = bot.Position - toEnemy.normalized * 5f;
                Vector3? navBased = HybridFallbackResolver.GetBestRetreatPoint(bot, toEnemy);
                Vector3 destination = navBased.HasValue ? AlignY(navBased.Value, bot.Position.y) : fallback;

                BotMovementHelper.SmoothMoveTo(bot, destination);
                if (bot.BotTalk != null)
                    bot.BotTalk.TrySay(EPhraseTrigger.OnLostVisual);
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

            if (bot.BotsGroup != null && bot.BotsGroup.LastSoundsController != null)
                bot.BotsGroup.LastSoundsController.AddNeutralSound(source, source.Position);

            BotMemoryStore.AddHeardSound(bot.ProfileId, source.Position, Time.time);

            Vector3 cautiousAdvance = source.Position + (bot.Position - source.Position).normalized * 3f;
            BotMovementHelper.SmoothMoveTo(bot, cautiousAdvance);

            if (bot.BotTalk != null)
                bot.BotTalk.TrySay(EPhraseTrigger.OnEnemyShot);
        }

        public static void ClearLastHeardSound(this BotOwner bot)
        {
            if (!IsValid(bot))
                return;

            BotMemoryStore.ClearHeardSound(bot.ProfileId);
        }

        #endregion

        #region Tactical Evaluation

        public static Vector3? TryGetFlankDirection(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory == null || bot.Memory.GoalEnemy == null)
                return null;

            Vector3 toEnemy = bot.Memory.GoalEnemy.CurrPosition - bot.Position;
            if (toEnemy.sqrMagnitude < 0.5f)
                return null;

            Vector3 botDir = bot.LookDirection.normalized;
            Vector3 enemyDir = toEnemy.normalized;

            float dot = Vector3.Dot(botDir, enemyDir);
            if (dot < 0.25f)
                return null;

            return Vector3.Cross(enemyDir, Vector3.up);
        }

        #endregion

        #region Helpers

        private static bool IsValid(BotOwner bot)
        {
            return bot != null &&
                   bot.GetPlayer != null &&
                   bot.GetPlayer.IsAI &&
                   !bot.GetPlayer.IsYourPlayer &&
                   !bot.IsDead;
        }

        private static Vector3 AlignY(Vector3 pos, float y)
        {
            return new Vector3(pos.x, y, pos.z);
        }

        #endregion
    }
}
