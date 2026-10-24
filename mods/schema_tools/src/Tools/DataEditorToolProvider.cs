namespace Cthangover.Tools.Schemas
{
    using Cthangover.Core.UI.Tool;
    using Cthangover.Mods.Interface.Tool;
    using Godot;

    /// <summary>Puts the data editor window into the tools menu.</summary>
    public class DataEditorToolProvider : IToolProvider
    {
        /// <inheritdoc />
        public string Id => "schema_data_editor";

        /// <inheritdoc />
        public string LocaleKey => "tools/schema/data_editor/title";

        /// <inheritdoc />
        public Window CreateWindow() => new DataEditorWindow();

        /// <inheritdoc />
        public void AddMenuButton(Container container) => ToolMenuButton.Attach(container, this);
    }
}
