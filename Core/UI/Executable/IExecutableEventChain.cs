namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// Marker interface for event chains. Currently empty — serves as a type
    /// discriminator for ExecutableEvent._Ready to decide whether the event
    /// should self-register with SceneEventController or be managed by its
    /// parent chain. Methods may be added as the chain abstraction grows.
    /// </summary>
    public interface IExecutableEventChain
    {

        

    }

}
