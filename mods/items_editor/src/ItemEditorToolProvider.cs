using Cthangover.Core.UI.Tool;
using Cthangover.Mods.Interface.Tool;
using Godot;

namespace Cthangover.Tools.ItemsEditor
{
    /// <summary>
    /// Registers the item editor as a tool: it appears in the main menu's tool
    /// block, the novel editor hub and (with <see cref="ItemEditorToolBoxButton"/>)
    /// the in-game toolbar.
    /// </summary>
    public class ItemEditorToolProvider : IToolProvider
    {
        /// <summary>Tool id.</summary>
        public string Id => "items_editor";

        /// <summary>Display name key.</summary>
        public string LocaleKey => "tools/items_editor/title";

        /// <summary>Creates the window.</summary>
        public Window CreateWindow() => new ItemEditorWindow();

        /// <summary>Adds the launcher button to the main menu's tool block.</summary>
        public void AddMenuButton(Container container) => ToolMenuButton.Attach(container, this);
    }
}
