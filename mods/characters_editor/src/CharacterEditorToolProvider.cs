using Cthangover.Core.UI.Tool;
using Cthangover.Mods.Interface.Tool;
using Godot;

namespace Cthangover.Tools.CharactersEditor
{
    /// <summary>
    /// Registers the character editor as a tool: it appears in the main menu's tool
    /// block, the novel editor hub and (with <see cref="CharacterEditorToolBoxButton"/>)
    /// the in-game toolbar.
    /// </summary>
    public class CharacterEditorToolProvider : IToolProvider
    {
        /// <summary>Tool id.</summary>
        public string Id => "characters_editor";

        /// <summary>Display name key.</summary>
        public string LocaleKey => "tools/characters_editor/title";

        /// <summary>Creates the window.</summary>
        public Window CreateWindow() => new CharacterEditorWindow();

        /// <summary>Adds the launcher button to the main menu's tool block.</summary>
        public void AddMenuButton(Container container) => ToolMenuButton.Attach(container, this);
    }
}
