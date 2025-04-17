#nullable enable

using System;
using EFT;

namespace AIRefactored.AI.Optimization
{
    /// <summary>
    /// Lightweight snapshot of a bot's aggression and perception state.
    /// Used for caching, comparison, or optimization layers.
    /// </summary>
    public class BotOwnerState
    {
        #region Properties

        /// <summary>
        /// Initial aggression coefficient (MIN_START_AGGRESION_COEF).
        /// </summary>
        public float Aggression { get; }

        /// <summary>
        /// Distance at which bot spots enemies from being hit (DIST_TO_ENEMY_SPOTTED_ON_HIT).
        /// </summary>
        public float PerceptionRange { get; }

        #endregion

        #region Constructor

        /// <summary>
        /// Constructs a snapshot of a bot's current AI aggression and perception tuning values.
        /// </summary>
        /// <param name="botOwner">The bot to snapshot.</param>
        public BotOwnerState(BotOwner botOwner)
        {
            if (botOwner?.Settings?.FileSettings?.Mind == null)
            {
                Aggression = 0f;
                PerceptionRange = 0f;
                return;
            }

            var mind = botOwner.Settings.FileSettings.Mind;
            Aggression = mind.MIN_START_AGGRESION_COEF;
            PerceptionRange = mind.DIST_TO_ENEMY_SPOTTED_ON_HIT;
        }

        #endregion

        #region Equality / Hashing

        /// <summary>
        /// Determines equality based on float tolerances for aggression and perception.
        /// </summary>
        public override bool Equals(object? obj)
        {
            return obj is BotOwnerState other &&
                   Math.Abs(Aggression - other.Aggression) < 0.01f &&
                   Math.Abs(PerceptionRange - other.PerceptionRange) < 0.1f;
        }

        /// <summary>
        /// Generates a hash code based on AI settings.
        /// </summary>
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + Aggression.GetHashCode();
                hash = hash * 23 + PerceptionRange.GetHashCode();
                return hash;
            }
        }

        #endregion
    }
}
