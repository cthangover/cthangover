namespace Cthangover.Core.UI.Executable
{
    public partial class IdentifiableEventObjectBehaviour : Godot.Node, IEventObject
    {
        public string ID { get; private set; }

        public void Destruct()
        {
            QueueFree();
        }
    }
}
