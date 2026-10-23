using Cthangover.Core.UI.Tool;
using Cthangover.Mods.Interface.Tool;
using Godot;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>
    /// Registers the RPG map editor as a tool: the kernel's tool factory finds
    /// every <see cref="IToolProvider"/>, so the editor appears in the main
    /// menu's tool block and (with
    /// <see cref="RpgMapEditorToolBoxButton"/>) the in-game toolbar.
    /// </summary>
    public class RpgMapEditorToolProvider : IToolProvider
    {
        /// <summary>Tool id.</summary>
        public string Id => "rpg_map_editor";

        /// <summary>Display name key.</summary>
        public string LocaleKey => "tools/rpg_editor/title";

        /// <summary>Creates the window.</summary>
        public Window CreateWindow() => new RpgMapEditorWindow();

        /// <summary>Adds the launcher button to the main menu's tool block.</summary>
        public void AddMenuButton(Container container) => ToolMenuButton.Attach(container, this);
    }
}
