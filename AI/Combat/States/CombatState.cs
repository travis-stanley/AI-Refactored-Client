#nullable enable

namespace AIRefactored.AI.Combat.States
{
    /// <summary>
    /// Describes the bot’s current combat behavior mode.
    /// Used by state handlers and the state machine.
    /// </summary>
    public enum CombatState
    {
        Patrol,
        Investigate,
        Engage,
        Attack,
        Fallback
    }
}
