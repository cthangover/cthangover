using System.Collections.Generic;

namespace Cthangover.Core.Mods.Testing
{
    /// <summary>Outcome of a single mod unit test (plan-mod-testing.md §7.2).</summary>
    public enum ModTestStatus
    {
        /// <summary>Test ran to completion without a failure.</summary>
        Passed,
        /// <summary>Test threw (assert or unexpected exception).</summary>
        Failed,
        /// <summary>Test declared skipped via <see cref="ModTestAttribute.Skip"/>.</summary>
        Skipped,
        /// <summary>Async test exceeded its per-test timeout.</summary>
        Timeout,
        /// <summary>Infrastructure error: compilation failure of the test assembly or recovery self-check violation.</summary>
        Error,
    }

    /// <summary>
    /// One recorded test outcome in the ledger. The test itself knows nothing
    /// about reporting: the engine records every outcome (including compile
    /// errors at mod granularity) and <see cref="ModTestReportWriter"/> is the
    /// only writer.
    /// </summary>
    public class ModTestEntry
    {
        /// <summary>Mod the test belongs to.</summary>
        public string ModId { get; set; }
        /// <summary>Report name of the test ("group/case").</summary>
        public string TestName { get; set; }
        /// <summary>Outcome.</summary>
        public ModTestStatus Status { get; set; }
        /// <summary>Wall-clock duration in milliseconds.</summary>
        public double DurationMs { get; set; }
        /// <summary>Assert/exception text; for Skipped — the skip reason.</summary>
        public string Message { get; set; }
        /// <summary>Stack trace for Failed/Error.</summary>
        public string StackTrace { get; set; }
        /// <summary>Lines written through <see cref="ModTestContext.Log"/> during this test.</summary>
        public IReadOnlyList<string> Log { get; set; } = new List<string>();
        /// <summary>Artifact paths inside the mod's SandboxDir recorded by the runner.</summary>
        public IReadOnlyList<string> Artifacts { get; set; } = new List<string>();
    }

    /// <summary>
    /// Per-mod summary line of a run: used by the console итоги block and the
    /// JSON report. A mod whose test assembly failed to compile gets
    /// <see cref="ModTestStatus.Error"/> with the Roslyn diagnostics.
    /// </summary>
    public class ModTestSuiteSummary
    {
        /// <summary>Mod id.</summary>
        public string ModId { get; set; }
        /// <summary>Aggregate status: the worst of the individual outcomes (or Error for compile failure).</summary>
        public ModTestStatus Status { get; set; }
        /// <summary>Passed test count.</summary>
        public int Passed { get; set; }
        /// <summary>Failed test count (includes Timeout).</summary>
        public int Failed { get; set; }
        /// <summary>Skipped test count.</summary>
        public int Skipped { get; set; }
        /// <summary>Error count (compile failure, recovery self-check violations).</summary>
        public int Errors { get; set; }
        /// <summary>Total suite duration in milliseconds.</summary>
        public double DurationMs { get; set; }
        /// <summary>Per-test entries in discovery order.</summary>
        public List<ModTestEntry> Tests { get; set; } = new();
        /// <summary>Mod version from the manifest, for the report header.</summary>
        public string Version { get; set; }
        /// <summary>Compilation cache hash of the test sources, to bind the report to the exact mod content.</summary>
        public string Hash { get; set; }
    }
}
