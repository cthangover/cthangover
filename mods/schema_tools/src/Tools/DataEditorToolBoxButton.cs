namespace Cthangover.Tools.Schemas
{
    using Cthangover.Core.UI.Tool;

    /// <summary>Puts the data editor window on the in-game tool box.</summary>
    public class DataEditorToolBoxButton : IToolBoxButton
    {
        /// <inheritdoc />
        public string ToolId => "schema_data_editor";

        /// <inheritdoc />
        public string IconPath => "";

        /// <inheritdoc />
        public string LocaleKey => "tools/schema/data_editor/title";

        /// <inheritdoc />
        public bool IsVisible() => true;
    }
}
