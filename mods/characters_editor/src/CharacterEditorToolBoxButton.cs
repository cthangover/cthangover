using Cthangover.Core.UI.Tool;

namespace Cthangover.Tools.CharactersEditor
{
    /// <summary>In-game toolbar button of the character editor, so a character can be fixed while playing.</summary>
    public class CharacterEditorToolBoxButton : IToolBoxButton
    {
        /// <summary>Matches <see cref="CharacterEditorToolProvider.Id"/>.</summary>
        public string ToolId => "characters_editor";

        /// <summary>No icon: the button shows its label.</summary>
        public string IconPath => "";

        /// <summary>Button label key.</summary>
        public string LocaleKey => "tools/characters_editor/title";

        /// <summary>Always shown.</summary>
        public bool IsVisible() => true;
    }
}
