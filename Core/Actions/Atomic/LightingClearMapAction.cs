namespace Cthangover.Core.Actions.Atomic
{
    public class LightingClearMapAction : IScenarioAction
    {
        public string Name => "lighting.clear_map";

        public void Run(IActionContext ctx)
        {
            ctx.Lighting.ClearDepthMap();
            ctx.Log("SHADER", "LightingClearMapAction: cleared depth and albedo maps");
        }
    }
}
