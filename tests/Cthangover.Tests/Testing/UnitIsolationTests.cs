namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using Cthangover.Core.Mods;

    /// <summary>
    /// Default path resolution of <see cref="ModLoadOrderStore"/> outside the
    /// Godot runtime: without the runner's private data root the order file is
    /// a bare relative file name in the current directory — never something
    /// anchored to the test assembly directory, which VSTest relocates between
    /// runs. The private-data-root branch is exercised end-to-end by the
    /// runner harness (TestExecutionContextRuntime injects CTHANGOVER_TEST_DATA
    /// into child component processes and verifies the store honors it), so it
    /// is not duplicated here by mutating the process-wide environment, which
    /// would race parallel test classes reading the same variable.
    /// </summary>
    public class UnitIsolationTests
    {
        private const string DataVariable = "CTHANGOVER_TEST_DATA";

        [Fact]
        public void ModOrder_DefaultsToBareRelativeFileName_WithoutPrivateDataRoot()
        {
            // The xunit host never sets the variable (it is only injected into
            // child component processes); assert that precondition so the
            // default branch is the one actually exercised.
            Assert.True(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(DataVariable)),
                "precondition: the xunit host process must not define " + DataVariable);

            var path = ModLoadOrderStore.Path;

            // Literal, not a copy of the production expression: a change of the
            // default file name, or a resolution that starts anchoring the file
            // to a directory, breaks this assertion.
            Assert.Equal("mod_load_order.json", path);
            Assert.Equal(Path.GetFileName(path), path);
        }
    }
}
