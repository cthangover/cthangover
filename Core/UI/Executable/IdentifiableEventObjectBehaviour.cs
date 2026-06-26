namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// Simplest IEventObject implementation: carries an ID and self-destructs
    /// via QueueFree when Destruct() is called. Used as a base for scene objects
    /// that must be tracked by the event system for cleanup.
    /// </summary>
    public partial class IdentifiableEventObjectBehaviour : Godot.Node, IEventObject
    {
        public string ID { get; private set; }

        public void Destruct()
        {
            QueueFree();
        }
    }
}
