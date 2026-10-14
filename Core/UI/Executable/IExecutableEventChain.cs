namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// Contract for event chains. ExecutableEvent uses it to decide whether the
    /// event should self-register with SceneEventController or be managed by its
    /// parent chain. The concrete chains (ExecutableEventListChain,
    /// ExecutableMainEventChain) live in the dialog_base engine mod; core code
    /// (SceneManager, SaveService) drives chains through this interface.
    /// </summary>
    public interface IExecutableEventChain
    {
        /// <summary>Registers an event with the chain and parents it to the chain node.</summary>
        void AddEvent(IExecutableEvent evt);

        /// <summary>Removes and frees all currently registered events.</summary>
        void ClearEvents();
    }
}