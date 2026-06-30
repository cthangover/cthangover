using Cthangover.Core.UI.Tool;
using Godot;

namespace Cthangover.Tools.InteractiveEditor
{
    /// <summary>
    /// Registers the interactive editor as a tool window in the developer toolbox.
    /// Implements <see cref="IToolProvider"/> so the tool system can instantiate
    /// <see cref="InteractiveEditorWindow"/> on demand.
    /// </summary>
    public class InteractiveEditorToolProvider : IToolProvider
    {
        /// <summary>Unique tool identifier referenced by the toolbox UI.</summary>
        public string Id => "interactive_editor";
        /// <summary>Translation key for the tool's display name.</summary>
        public string LocaleKey => "tools/interactive_editor/title";
        /// <summary>Creates the editor window instance.</summary>
        public Window CreateWindow() => new InteractiveEditorWindow();
    }

    /// <summary>
    /// Provides a button in the developer toolbox that opens the interactive editor.
    /// Controls visibility via the <see cref="IsVisible"/> policy.
    /// </summary>
    public class InteractiveEditorToolBoxButton : IToolBoxButton
    {
        /// <summary>Matches <see cref="InteractiveEditorToolProvider.Id"/>.</summary>
        public string ToolId => "interactive_editor";
        /// <summary>No custom icon — uses the tool title text.</summary>
        public string IconPath => "";
        /// <summary>Translation key for the button label.</summary>
        public string LocaleKey => "tools/interactive_editor/title";
        /// <summary>Always visible in the toolbox.</summary>
        public bool IsVisible() => true;
    }
}
