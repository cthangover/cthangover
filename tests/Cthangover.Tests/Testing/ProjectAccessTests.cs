namespace Cthangover.Tests.Testing
{
    using System;
    using System.Text.Json;
    using Cthangover.TestRunner;

    /// <summary>Access claims and output ownership survive JSON and are rejected before a shared snapshot can start.</summary>
    public class ProjectAccessTests
    {
        [Theory]
        [InlineData("read")]
        [InlineData("output")]
        [InlineData("write")]
        public void ExplicitAccessAndDurationRoundTrip(string access)
        {
            var test = Entry("first", access, "captures");
            test.Seconds = 12.5;
            var json = JsonSerializer.Serialize(test, ReportWriter.JsonOptions);
            var copy = JsonSerializer.Deserialize<IntegrationTestDefinition>(json, ReportWriter.JsonOptions);
            ProjectAccess.Validate(copy);
            Assert.Equal(access, copy.Project);
            Assert.Equal(12.5, copy.Seconds);
            Assert.Equal(json, JsonSerializer.Serialize(copy, ReportWriter.JsonOptions));
        }

        [Fact]
        public void AbsentAccessIsConservativeAndUnknownOrUndeclaredOutputIsRejected()
        {
            Assert.Equal("write", JsonSerializer.Deserialize<IntegrationTestDefinition>("{}").Project);
            var test = Entry("first", "unknown", "captures");
            Assert.Throws<InvalidOperationException>(() => ProjectAccess.Validate(test));
            test.Project = "output";
            test.Args = Array.Empty<string>();
            Assert.Throws<InvalidOperationException>(() => ProjectAccess.Validate(test));
            test.Args = new[] { "--shots=res://tests/artifacts/../outside" };
            Assert.Throws<InvalidOperationException>(() => ProjectAccess.Validate(test));
        }

        [Theory]
        [InlineData("captures")]
        [InlineData("captures/child")]
        [InlineData("CAPTURES")]
        public void OverlappingScopesRequireOneDependencyComponent(string other)
        {
            var first = Entry("first", "output", "captures");
            var second = Entry("second", "output", other);
            Assert.Throws<InvalidOperationException>(() => ProjectAccess.ValidateOutputs(new[] { first, second }));
            second.DependsOn = new[] { "first" };
            ProjectAccess.ValidateOutputs(new[] { first, second });
        }

        [Fact]
        public void SiblingScopesAreIndependent()
            => ProjectAccess.ValidateOutputs(new[] { Entry("first", "output", "captures/first"), Entry("second", "output", "captures/second") });

        private static IntegrationTestDefinition Entry(string name, string access, string output) => new()
        { Name = name, Project = access, Args = new[] { "--shots=res://tests/artifacts/" + output } };
    }
}
