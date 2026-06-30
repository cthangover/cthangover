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
        /// <summary>
        /// Registered as "scene.instantiate" — loads a PackedScene from a
        /// Godot resource path and inserts it into the current scene tree
        /// at runtime. The "path" variable is a Godot resource path (e.g.
        /// "res://scenes/SomeObject.tscn"), "name" sets the node's Name
        /// property. The node becomes a child of SceneContextNode.Instance
        /// and is visible immediately. Both variables are required.
        /// </summary>
        public string Name => "scene.instantiate";

        /// <summary>
        /// Reads "path" and "name" from dialog variables and delegates to
        /// ctx.Scene.Instantiate. Uses GD.Load&lt;PackedScene&gt;
        /// internally — if the resource fails to load (wrong path), an
        /// error is logged but the dialog continues. Returns early with a
        /// warning if either variable is missing.
        /// </summary>
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
