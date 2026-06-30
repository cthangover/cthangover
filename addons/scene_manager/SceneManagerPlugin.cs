#if TOOLS
using Godot;

namespace SceneManagerAddon
{
    /// <summary>
    /// The Godot EditorPlugin entry point for the Scene Manager addon.
    /// On activation (<see cref="_EnterTree"/>), it instantiates a
    /// <see cref="MainPanel"/>, docks it as a bottom-panel tab labelled
    /// "Scene Manager", and triggers the initial data load and
    /// validation. On deactivation (<see cref="_ExitTree"/>), it removes
    /// and frees the panel. The class is wrapped in <c>#if TOOLS</c>
    /// so it is never compiled into release builds.
    /// </summary>
    [Tool]
    public partial class SceneManagerPlugin : EditorPlugin
    {
        private MainPanel _panel;

        /// <summary>
        /// Called by the Godot editor when the plugin is activated.
        /// Creates the <see cref="MainPanel"/>, registers it as a
        /// bottom-panel tab, and triggers <see cref="MainPanel.Refresh"/>
        /// to load all data and populate the views.
        /// </summary>
        public override void _EnterTree()
        {
            _panel = new MainPanel { Name = "SceneManagerPanel" };
#pragma warning disable CS0618
            AddControlToBottomPanel(_panel, "Scene Manager");
#pragma warning restore CS0618
            _panel.Refresh();
        }

        /// <summary>
        /// Called by the Godot editor when the plugin is deactivated.
        /// Removes the panel from the bottom dock and queues it for
        /// freeing to prevent memory leaks.
        /// </summary>
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
