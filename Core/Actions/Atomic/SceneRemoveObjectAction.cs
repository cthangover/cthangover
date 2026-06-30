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
        /// <summary>
        /// Registered as "scene.remove_object" — removes a named child
        /// node from the scene tree. Before freeing the node, it notifies
        /// the event system via SceneContextNode.RemoveEventObject to
        /// clean up subscriptions. The "name" variable must match the
        /// node's Name property exactly — partial matches won't work.
        /// </summary>
        public string Name => "scene.remove_object";

        /// <summary>
        /// Reads the "name" variable and delegates to ctx.Scene.Remove.
        /// Safe to call on non-existent nodes — the implementation
        /// silently returns if the named child is not found. Returns
        /// early with a warning if "name" is missing.
        /// </summary>
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
