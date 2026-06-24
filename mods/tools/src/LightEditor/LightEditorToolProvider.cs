using Cthangover.Core.UI.Tool;
using Godot;

namespace Cthangover.Core.UI.Tool.LightEditor
{
    public class LightEditorToolProvider : IToolProvider
    {
        public string Id => "light_editor";
        public string LocaleKey => "tools/light_editor/title";
        public Window CreateWindow() => new LightEditorWindow();
    }

    public class LightEditorToolBoxButton : IToolBoxButton
    {
        public string ToolId => "light_editor";
        public string IconPath => "";
        public string LocaleKey => "tools/light_editor/title";
        public bool IsVisible() => true;
    }
}
