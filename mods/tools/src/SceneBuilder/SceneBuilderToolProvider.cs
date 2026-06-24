using Cthangover.Core.UI.Tool;
using Godot;

namespace Cthangover.Core.UI.Tool.SceneBuilder
{
    public class SceneBuilderToolProvider : IToolProvider
    {
        public string Id => "scene_builder";
        public string LocaleKey => "tools/scene_builder/title";
        public Window CreateWindow() => new SceneBuilderWindow();
    }
}
