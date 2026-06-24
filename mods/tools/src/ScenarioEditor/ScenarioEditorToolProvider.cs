using Cthangover.Core.UI.Tool;
using Godot;

namespace Cthangover.Core.UI.Tool.ScenarioEditor
{
    public class ScenarioEditorToolProvider : IToolProvider
    {
        public string Id => "scenario_editor";
        public string LocaleKey => "tools/scenario_editor/title";
        public Window CreateWindow() => new ScenarioEditorWindow();
    }
}
