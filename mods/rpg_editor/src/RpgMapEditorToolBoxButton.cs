using Cthangover.Core.UI.Tool;

namespace Cthangover.Tools.RpgEditor
{
    /// <summary>In-game toolbar button of the RPG map editor, so a map can be fixed while walking on it.</summary>
    public class RpgMapEditorToolBoxButton : IToolBoxButton
    {
        /// <summary>Matches <see cref="RpgMapEditorToolProvider.Id"/>.</summary>
        public string ToolId => "rpg_map_editor";

        /// <summary>No icon: the button shows its label.</summary>
        public string IconPath => "";

        /// <summary>Button label key.</summary>
        public string LocaleKey => "tools/rpg_editor/title";

        /// <summary>Always shown.</summary>
        public bool IsVisible() => true;
    }
}
