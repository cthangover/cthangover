using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Pure parts of the mod test engine: how a test failure surfaces and how
    /// a run is aggregated into the CI contract and the exit code.
    /// </summary>
    public class ModTestEngineTests
    {
        private static void FailingAssert() => ModAssert.Equal(1, 2, "one equals two");

        private static void ThrowsPlain(ModTestContext ctx) => throw new InvalidOperationException("boom from the test");

        [Fact]
        public void Invoke_SyncAssertFailure_SurfacesAssertException()
        {
            // Reflection wraps a synchronous throw in TargetInvocationException;
            // the report must show the assertion, not "Exception has been
            // thrown by the target of an invocation".
            var method = typeof(ModTestEngineTests).GetMethod(nameof(FailingAssert), BindingFlags.NonPublic | BindingFlags.Static);

            var ex = Assert.Throws<ModAssertException>(() => ModTestRunner.Invoke(method, null));
            Assert.Contains("one equals two", ex.Message);
            Assert.Contains(nameof(FailingAssert), ex.StackTrace);
        }

        [Fact]
        public void Invoke_SyncException_WithContextParameter_KeepsOriginalType()
        {
            var method = typeof(ModTestEngineTests).GetMethod(nameof(ThrowsPlain), BindingFlags.NonPublic | BindingFlags.Static);

            var ex = Assert.Throws<InvalidOperationException>(() => ModTestRunner.Invoke(method, null));
            Assert.Equal("boom from the test", ex.Message);
        }

        [Fact]
        public void FormatDiagnostic_UsesPathLineColumn()
        {
            // Compile errors must be clickable "file(line,col)" locations, not
            // Roslyn's default character span "SourceFile(path[3904..3923))".
            var tree = CSharpSyntaxTree.ParseText("class A\n{\n    void M() { Missing(); }\n}\n", path: "/mods/demo/tests/A.cs");
            var compilation = CSharpCompilation.Create("demo", new[] { tree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var error = compilation.GetDiagnostics().First(d => d.Severity == DiagnosticSeverity.Error);

            Assert.Equal("/mods/demo/tests/A.cs(3,16): error CS0103: " + error.GetMessage(), ModCompiler.FormatDiagnostic(error));
        }

        [Fact]
        public void ComputeTotals_SkipsDoNotFailTheRun()
        {
            var totals = ModTestReportWriter.ComputeTotals(new List<ModTestSuiteSummary>
            {
                new() { ModId = "a", Passed = 1, Skipped = 3 },
            });

            Assert.Equal(1, totals.Passed);
            Assert.Equal(0, totals.Failed);
            Assert.Equal(3, totals.Skipped);
            Assert.True(totals.Success);
        }

        [Fact]
        public void ComputeTotals_FailuresAndErrorsFailTheRun()
        {
            var totals = ModTestReportWriter.ComputeTotals(new List<ModTestSuiteSummary>
            {
                new() { ModId = "a", Passed = 2, Failed = 1, Skipped = 1 },
                new() { ModId = "b", Passed = 0, Errors = 1 },
            });

            Assert.Equal(2, totals.Passed);
            Assert.Equal(2, totals.Failed);
            Assert.Equal(1, totals.Skipped);
            Assert.False(totals.Success);
        }
    }
}
