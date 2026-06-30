using Cthangover.Core.UI.Tool;
using Godot;

namespace Cthangover.Core.UI.Tool.LightEditor
{
    /// <summary>Registers the light editor as a tool window. Creates <see cref="LightEditorWindow"/> instances.</summary>
    public class LightEditorToolProvider : IToolProvider
    {
        /// <summary>Unique tool identifier.</summary>
        public string Id => "light_editor";
        /// <summary>Translation key for the tool's display name.</summary>
        public string LocaleKey => "tools/light_editor/title";
        /// <summary>Creates the editor window instance.</summary>
        public Window CreateWindow() => new LightEditorWindow();
    }

    /// <summary>Toolbox button that opens the light editor. Always visible.</summary>
    public class LightEditorToolBoxButton : IToolBoxButton
    {
        /// <summary>Matches <see cref="LightEditorToolProvider.Id"/>.</summary>
        public string ToolId => "light_editor";
        /// <summary>No custom icon — uses text label.</summary>
        public string IconPath => "";
        /// <summary>Translation key for the button label.</summary>
        public string LocaleKey => "tools/light_editor/title";
        /// <summary>Always visible in the toolbox.</summary>
        public bool IsVisible() => true;
    }
}
