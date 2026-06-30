using Cthangover.Core.UI.Lights;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Thin wrapper around UiLightController.Instance. ClearDepthMap sets both
    /// depth and albedo to null simultaneously — partial clearing would leave
    /// the shader in an inconsistent state. SetUseTime delegates to the
    /// controller's IsUseLight property.
    /// </summary>
    internal class LightingServiceImpl : ILightingService
    {
        /// <summary>
        /// Sets both depth and albedo maps to null on UiLightController.
        /// Both are cleared together because the lighting shader requires
        /// either both textures present or neither — partial clearing
        /// produces visual artifacts. Safe to call when the controller
        /// hasn't been initialized yet (null-conditional access).
        /// </summary>
        public void ClearDepthMap()
        {
            var controller = UiLightController.Instance;
            controller?.SetupDepthMap(null);
            controller?.SetupAlbedoMap(null);
        }

        /// <summary>
        /// Toggles the IsUseLight property on UiLightController. When
        /// true, the controller applies time-of-day lighting adjustments;
        /// when false, lighting remains static. Null-safe: if the
        /// controller singleton is absent (pre-initialization), the call
        /// is silently skipped.
        /// </summary>
        public void SetUseTime(bool useTime)
        {
            var controller = UiLightController.Instance;
            if (controller != null)
                controller.IsUseLight = useTime;
        }
    }
}
