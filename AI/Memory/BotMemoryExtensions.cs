#nullable enable

namespace AIRefactored.AI.Memory
{
    using AIRefactored.AI.Helpers;
    using AIRefactored.AI.Optimization;

    using EFT;

    using UnityEngine;

    /// <summary>
    /// Tactical extensions for BotOwner memory and behavior.
    /// Provides smart fallback, perception updates, state toggles, and auditory reactions.
    /// </summary>
    public static class BotMemoryExtensions
    {
        public static void ClearLastHeardSound(this BotOwner bot)
        {
            if (!IsValid(bot))
                return;

            BotMemoryStore.ClearHeardSound(bot.ProfileId);
        }

        public static void FallbackTo(this BotOwner bot, Vector3 fallbackPosition)
        {
            if (!IsValid(bot) || fallbackPosition.sqrMagnitude < 0.5f)
                return;

            var cache = BotCacheUtility.GetCache(bot);
            if (cache?.PanicHandler?.IsPanicking == true)
                return;

            BotMovementHelper.SmoothMoveTo(bot, fallbackPosition);
        }

        public static void ForceMoveTo(this BotOwner bot, Vector3 position)
        {
            if (!IsValid(bot) || position.sqrMagnitude < 0.5f)
                return;

            BotMovementHelper.SmoothMoveTo(bot, position);
        }

        /// <summary>
        /// Triggers fallback movement if bot is in poor cover facing visible enemy.
        /// Updated to use BotCoverRetreatPlanner instead of HybridFallbackResolver.
        /// </summary>
        public static void ReevaluateCurrentCover(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory?.GoalEnemy?.IsVisible != true)
                return;

            Vector3 toEnemy = bot.Memory.GoalEnemy.CurrPosition - bot.Position;
            if (toEnemy.sqrMagnitude < 1f)
                return;

            float angle = Vector3.Angle(bot.LookDirection, toEnemy.normalized);
            if (angle < 20f && toEnemy.sqrMagnitude < 625f)
            {
                Vector3 fallback = bot.Position - toEnemy.normalized * 5f;
                Vector3 destination = fallback;

                var cache = BotCacheUtility.GetCache(bot);
                if (cache?.PathCache != null)
                {
                    var path = BotCoverRetreatPlanner.GetCoverRetreatPath(bot, toEnemy, cache.PathCache);
                    if (path.Count > 0)
                        destination = path[path.Count - 1];
                }

                destination = AlignY(destination, bot.Position.y);
                BotMovementHelper.SmoothMoveTo(bot, destination);
                bot.BotTalk?.TrySay(EPhraseTrigger.OnLostVisual);
            }
        }

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

        public static void SetLastHeardSound(this BotOwner bot, Player source)
        {
            if (!IsValid(bot) || source == null || source.ProfileId == bot.ProfileId)
                return;

            bot.BotsGroup?.LastSoundsController?.AddNeutralSound(source, source.Position);
            BotMemoryStore.AddHeardSound(bot.ProfileId, source.Position, Time.time);

            Vector3 cautiousAdvance = source.Position + (bot.Position - source.Position).normalized * 3f;
            BotMovementHelper.SmoothMoveTo(bot, cautiousAdvance);

            bot.BotTalk?.TrySay(EPhraseTrigger.OnEnemyShot);
        }

        public static void SetPeaceMode(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory == null)
                return;

            bot.Memory.AttackImmediately = false;
            bot.Memory.IsPeace = true;
            bot.Memory.CheckIsPeace();
        }

        public static Vector3? TryGetFlankDirection(this BotOwner bot)
        {
            if (!IsValid(bot) || bot.Memory?.GoalEnemy == null)
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

        private static Vector3 AlignY(Vector3 pos, float y)
        {
            return new Vector3(pos.x, y, pos.z);
        }

        private static bool IsValid(BotOwner bot)
        {
            return bot != null && bot.GetPlayer != null && bot.GetPlayer.IsAI && !bot.GetPlayer.IsYourPlayer
                   && !bot.IsDead;
        }
    }
}