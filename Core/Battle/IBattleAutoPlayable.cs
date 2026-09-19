namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Optional automation contract for battle cores. The base
    /// <see cref="IBattleCore"/> contract describes how a battle starts but
    /// not how it advances: every core owns its own UI and waits for player
    /// input in its own way. Integration tests and AI tooling therefore ask
    /// the core itself when it waits for the player and how to advance the
    /// turn, instead of hunting for core-specific buttons by node path.
    /// Cores that don't implement this interface are still runnable but are
    /// reported as not universally testable by the battle coverage tool.
    /// </summary>
    public interface IBattleAutoPlayable
    {
        /// <summary>
        /// <c>true</c> while the core accepts a player turn advance. Must
        /// become <c>false</c> immediately when the turn ends, so a polling
        /// driver cannot trigger two turns in a row.
        /// </summary>
        bool IsWaitingForPlayerInput { get; }

        /// <summary>
        /// Ends the current player turn through the same path the core's UI
        /// uses (skipping the player's actions is a valid strategy for
        /// smoke tests). Must be a no-op when
        /// <see cref="IsWaitingForPlayerInput"/> is <c>false</c>.
        /// </summary>
        void AdvancePlayerTurn();
    }
}
