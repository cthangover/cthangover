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
        public void ClearDepthMap()
        {
            var controller = UiLightController.Instance;
            controller?.SetupDepthMap(null);
            controller?.SetupAlbedoMap(null);
        }

        public void SetUseTime(bool useTime)
        {
            var controller = UiLightController.Instance;
            if (controller != null)
                controller.IsUseLight = useTime;
        }
    }
}
