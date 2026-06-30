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
        /// <summary>
        /// Registered as "lighting.clear_map" — resets the depth and albedo
        /// lighting maps to null via UiLightController. Both maps are
        /// cleared simultaneously because the lighting shader requires
        /// either both present or neither — partial clearing produces
        /// visual artifacts. Use when transitioning between scenes that
        /// have different lighting setups, or when entering a scene that
        /// doesn't use depth-based lighting.
        /// </summary>
        public string Name => "lighting.clear_map";

        /// <summary>
        /// Delegates to ctx.Lighting.ClearDepthMap which nulls out both
        /// depth and albedo textures on the UiLightController singleton.
        /// Safe to call when the controller hasn't been initialized
        /// (null-conditional access in the implementation).
        /// </summary>
        public void Run(IActionContext ctx)
        {
            ctx.Lighting.ClearDepthMap();
            ctx.Log("SHADER", "LightingClearMapAction: cleared depth and albedo maps");
        }
    }
}
