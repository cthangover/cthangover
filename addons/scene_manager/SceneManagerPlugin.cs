#if TOOLS
using Godot;

namespace SceneManagerAddon
{
    [Tool]
    public partial class SceneManagerPlugin : EditorPlugin
    {
        private MainPanel _panel;

        public override void _EnterTree()
        {
            _panel = new MainPanel { Name = "SceneManagerPanel" };
#pragma warning disable CS0618
            AddControlToBottomPanel(_panel, "Scene Manager");
#pragma warning restore CS0618
            _panel.Refresh();
        }

        public override void _ExitTree()
        {
#pragma warning disable CS0618
            RemoveControlFromBottomPanel(_panel);
#pragma warning restore CS0618
            _panel?.QueueFree();
        }
    }
}
#endif
