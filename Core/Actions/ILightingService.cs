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
        void ClearDepthMap();
        void SetUseTime(bool useTime);
    }
}