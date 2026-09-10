namespace Cthangover.Core.UI.Executable
{
    /// <summary>
    /// Simplest IEventObject implementation: carries an ID and self-destructs
    /// via QueueFree when Destruct() is called. Used as a base for scene objects
    /// that must be tracked by the event system for cleanup.
    /// </summary>
    public partial class IdentifiableEventObjectBehaviour : Godot.Node, IEventObject
    {
        /// <summary>
        /// Identifier for this event object. Set externally to allow the event
        /// tracking system to locate and manage this instance.
        /// </summary>
        public string ID { get; private set; }

        /// <summary>
        /// Signals cleanup by calling <c>QueueFree</c> to remove this node from
        /// the scene tree and release resources.
        /// </summary>
        public void Destruct()
        {
            QueueFree();
        }
    }
}
