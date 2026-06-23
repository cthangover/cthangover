namespace Cthangover.Core.Actions.Atomic
{
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
