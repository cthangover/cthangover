namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Resets the lighting depth and albedo maps to null via the lighting
    /// service. This effectively disables scene-specific lighting masks,
    /// reverting to default flat lighting. Used when transitioning between
    /// scenes that have different lighting setups or when entering a scene
    /// that doesn't use depth-based lighting.
    /// </summary>
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
