using Cthangover.Core.UI.Lights;

namespace Cthangover.Core.Actions
{
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
