using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    public class BootControllerTests
    {
        [Fact]
        public void IsLauncherBoot_OutsideRuntime_IsFalse()
        {
            Assert.False(BootController.IsLauncherBoot);
        }

        [Fact]
        public void MarkModsInitialized_ReleasesWaiters()
        {
            BootController.MarkModsInitialized();

            Assert.True(BootController.ModsInitialized);
            Assert.True(BootController.WaitForModsInitialized(0));
        }
    }
}
