namespace Cthangover.Tools.Schemas
{
    using Cthangover.Core.UI.Tool;

    /// <summary>Puts the command palette on the in-game tool box.</summary>
    public class PaletteToolBoxButton : IToolBoxButton
    {
        /// <inheritdoc />
        public string ToolId => "schema_palette";

        /// <inheritdoc />
        public string IconPath => "";

        /// <inheritdoc />
        public string LocaleKey => "tools/schema/palette/title";

        /// <inheritdoc />
        public bool IsVisible() => true;
    }
}
