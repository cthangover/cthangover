using Godot;

namespace Cthangover.Core.UI
{
    public partial class InputHandlerNode : Node
    {
        public override void _Input(InputEvent @event)
        {
            OnInput(@event);
        }

        protected virtual void OnInput(InputEvent @event) { }
    }
}
