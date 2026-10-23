using Cthangover.Core.UI.Tool;

namespace Cthangover.Tools.ItemsEditor
{
    /// <summary>In-game toolbar button of the item editor, so an item can be fixed while playing.</summary>
    public class ItemEditorToolBoxButton : IToolBoxButton
    {
        /// <summary>Matches <see cref="ItemEditorToolProvider.Id"/>.</summary>
        public string ToolId => "items_editor";

        /// <summary>No icon: the button shows its label.</summary>
        public string IconPath => "";

        /// <summary>Button label key.</summary>
        public string LocaleKey => "tools/items_editor/title";

        /// <summary>Always shown.</summary>
        public bool IsVisible() => true;
    }
}
