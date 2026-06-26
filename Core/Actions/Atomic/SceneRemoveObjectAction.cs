namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Removes a named child from the scene tree. Before RemoveChild+QueueFree,
    /// it calls SceneContextNode.RemoveEventObject to notify the event system
    /// that the object is being destroyed — this prevents stale references in
    /// the event chain. The "name" must match the node's Name property exactly.
    /// </summary>
    public class SceneRemoveObjectAction : IScenarioAction
    {
        public string Name => "scene.remove_object";

        public void Run(IActionContext ctx)
        {
            var name = ctx.GetParam("name");

            if (string.IsNullOrEmpty(name))
            {
                ctx.Log("EVENT", "SceneRemoveObjectAction: missing 'name' variable");
                return;
            }

            ctx.Scene.Remove(name);
            ctx.Log("SCENE", $"SceneRemoveObjectAction: removed '{name}'");
        }
    }
}
