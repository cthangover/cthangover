using System.Threading;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    public class ModRegistryTests
    {
        [Fact]
        public void DiscoverAndCompile_PreCancelledToken_Throws()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(
                () => ModRegistry.Instance.DiscoverAndCompile(cts.Token));
        }
    }
}
