namespace Cthangover.Core.Actions.Atomic
{
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
