namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Lighting control contract for scenario actions. ClearDepthMap resets the
    /// depth/albedo textures on the UiLightController singleton, effectively
    /// removing scene-specific lighting masks. SetUseTime toggles time-of-day
    /// lighting on/off.
    /// </summary>
    public interface ILightingService
    {
        /// <summary>
        /// Resets both the depth texture and albedo texture on the
        /// UiLightController singleton to null. This disables scene-specific
        /// lighting masks, reverting the shader to default flat lighting.
        /// Both maps are cleared together because partial clearing would
        /// leave the lighting shader in an inconsistent state. Used during
        /// scene transitions — call before entering a scene that doesn't
        /// use depth-based lighting, or when switching between scenes with
        /// different lighting setups.
        /// </summary>
        void ClearDepthMap();

        /// <summary>
        /// Enables or disables time-of-day lighting on the UiLightController.
        /// When enabled, the controller adjusts scene lighting based on the
        /// in-game time; when disabled, lighting remains static. Use this
        /// when entering interior scenes (disable) or exterior scenes
        /// (enable) to match the environmental context.
        /// </summary>
        void SetUseTime(bool useTime);
    }
}