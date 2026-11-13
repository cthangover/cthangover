namespace Cthangover.Tests.Boot
{
    using System;
    using Cthangover.Core.Bootstrap;
    using Xunit;

    /// <summary>The CLR check rejects affected servicing versions before a mod can unload.</summary>
    public class RuntimeRequirementsTests
    {
        [Fact]
        public void TestHostUsesTheFixedRuntime()
            => Assert.Null(RuntimeRequirements.GetProblem(Environment.Version));

        [Theory]
        [InlineData(8, 0, 32)]
        [InlineData(8, 0, 34)]
        [InlineData(9, 0, 23)]
        public void AffectedRuntimeHasInstallationAndLaunchInstructions(int major, int minor, int build)
        {
            var problem = RuntimeRequirements.GetProblem(new Version(major, minor, build));
            Assert.Contains(".NET 10", problem);
            Assert.Contains("net10.0", problem);
            Assert.Contains(".NET 10 SDK", problem);
        }

        [Theory]
        [InlineData(10, 0, 0)]
        [InlineData(10, 0, 15)]
        public void FixedRuntimeCanBoot(int major, int minor, int build)
            => Assert.Null(RuntimeRequirements.GetProblem(new Version(major, minor, build)));

        [Fact]
        public void MissingRuntimeIsRejected()
            => Assert.NotNull(RuntimeRequirements.GetProblem(null));
    }
}
