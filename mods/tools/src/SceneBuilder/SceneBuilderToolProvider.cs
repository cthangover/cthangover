using Cthangover.Core.UI.Tool;
using Godot;

namespace Cthangover.Core.UI.Tool.SceneBuilder
{
    /// <summary>Registers the scene builder as a tool window. Creates <see cref="SceneBuilderWindow"/> instances.</summary>
    public class SceneBuilderToolProvider : IToolProvider
    {
        /// <summary>Unique tool identifier.</summary>
        public string Id => "scene_builder";
        /// <summary>Translation key for the tool's display name.</summary>
        public string LocaleKey => "tools/scene_builder/title";
        /// <summary>Creates the editor window instance.</summary>
        public Window CreateWindow() => new SceneBuilderWindow();
    }
}
