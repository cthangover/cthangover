namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Instantiates a PackedScene into the current scene tree at runtime.
    /// The "path" is a Godot resource path (e.g. "res://scenes/SomeObject.tscn"),
    /// "name" sets the node's Name property. The instantiated node becomes a
    /// child of SceneContextNode.Instance (the current scene root), making it
    /// visible immediately.
    /// </summary>
    public class SceneInstantiateAction : IScenarioAction
    {
        public string Name => "scene.instantiate";

        public void Run(IActionContext ctx)
        {
            var path = ctx.GetParam("path");
            var name = ctx.GetParam("name");

            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(name))
            {
                ctx.Log("EVENT", "SceneInstantiateAction: missing 'path' or 'name' variable");
                return;
            }

            ctx.Scene.Instantiate(path, name);
            ctx.Log("SCENE", $"SceneInstantiateAction: instantiated '{path}' as '{name}'");
        }
    }
}
