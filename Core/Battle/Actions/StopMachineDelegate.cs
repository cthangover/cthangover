namespace Cthangover.Core.Battle.Actions
{
    /// <summary>
    /// Signal emitted when the action machine drains or is stopped.
    /// Battle cores subscribe to this to advance to the next phase
    /// (e.g. end turn, start enemy turn) without polling.
    /// </summary>
    public delegate void StopMachineDelegate();
}
