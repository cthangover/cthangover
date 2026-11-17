namespace Cthangover.TestRunner
{
    using System.Text.Json.Serialization;
    /// <summary>
    /// Aggregated result of one suite entry (unit or a single integration
    /// scene) shown in the final summary and serialized to JSON. Check counts
    /// come from TRX for xUnit and from <c>[TEST_RESULT]</c> for scenes;
    /// <see cref="ExitCode"/>, <see cref="TimedOut"/> and <see cref="Note"/>
    /// keep the diagnostic context when a run fails.
    /// </summary>
    public class TestResult
    {
        /// <summary>Catalogue identity retained across independent launch attempts.</summary>
        public string Name
        {
            get; set;
        }
        /// <summary>Authoritative parent-owned launch identity, isolated paths and actual launch input transfers.</summary>
        public LaunchExecutionMetrics Execution
        {
            get; set;
        }
        /// <summary>Execution adapter used to interpret this result.</summary>
        public string Kind
        {
            get; set;
        }
        /// <summary>Parent verdict after terminal, process, context and immutable-input checks.</summary>
        [JsonPropertyName("success")]
        public bool Passed
        {
            get; set;
        }
        /// <summary>
        /// Records parent-enforced timeout even when the child emitted an earlier success marker.
        /// </summary>
        public bool TimedOut
        {
            get; set;
        }
        /// <summary>Observed process exit or infrastructure sentinel, never inferred from a PASS line.</summary>
        public int ExitCode
        {
            get; set;
        }
        /// <summary>Verified successful assertions from the authoritative native report or TRX.</summary>
        [JsonPropertyName("passed")]
        public int PassedChecks
        {
            get; set;
        }
        /// <summary>
        /// Authoritative failed assertions retained independently of later markers or process exit.
        /// </summary>
        [JsonPropertyName("failed")]
        public int FailedChecks
        {
            get; set;
        }
        /// <summary>
        /// Authoritative skipped assertions, never counted as successful checks.
        /// </summary>
        [JsonPropertyName("skipped")]
        public int SkippedChecks
        {
            get; set;
        }
        /// <summary>
        /// Indicates admission prevented a launch because prerequisites or infrastructure were unsuccessful.
        /// </summary>
        public bool Blocked
        {
            get; set;
        }
        /// <summary>Case selection expression actually forwarded to this launch.</summary>
        public string Filter
        {
            get; set;
        }
        /// <summary>Explicit mod-unit host forwarded to the child.</summary>
        public string ModFilter
        {
            get; set;
        }
        /// <summary>Dependency component identity whose ordered save/settings state this launch uses.</summary>
        public string Environment
        {
            get; set;
        }
        /// <summary>Explains direct selection versus prerequisite closure in the persisted plan.</summary>
        public string SelectionReason
        {
            get; set;
        }
        /// <summary>Prerequisite identities preserved for failure replay and blocked-result diagnostics.</summary>
        public string[] DependsOn { get; set; } = Array.Empty<string>();
        /// <summary>
        /// Potential sources of an aggregate infrastructure violation; --failed repeats their original selections.
        /// </summary>
        public string[] CandidateLaunches { get; set; } = Array.Empty<string>();
        /// <summary>Detached original per-case outcomes from native JSON or VSTest.</summary>
        public List<TestCaseResult> Cases { get; set; } = new();
        /// <summary>Durable files belonging only to this launch or its captured failed write delta.</summary>
        public List<string> Artifacts { get; set; } = new();
        /// <summary>Derived presentation label; it cannot convert an incomplete or failed contract into success.</summary>
        public string Status => Blocked ? "blocked" : TimedOut ? "timeout" : !Passed ? "fail"
            : PassedChecks == 0 && SkippedChecks > 0 ? "skipped" : "pass";
        /// <summary>Measured process wall interval; preparation and artifact overhead are separate.</summary>
        [JsonIgnore]
        public TimeSpan Duration
        {
            get; set;
        }
        /// <summary>
        /// Portable numeric child wall interval; preparation and collection overhead are recorded separately.
        /// </summary>
        public double DurationSeconds
        {
            get => Duration.TotalSeconds; set => Duration = TimeSpan.FromSeconds(value);
        }
        /// <summary>Project restore before this launch plus artifact collection afterwards; excludes report exports.</summary>
        public double OverheadSeconds
        {
            get; set;
        }
        /// <summary>Process start to first stdout TEST/MODTEST category; null when no driver category was observed.</summary>
        public double? BootSeconds
        {
            get; set;
        }
        /// <summary>Best-effort sampled CPU seconds of the launched root process, not an aggregate of descendants.</summary>
        public double? CpuSeconds
        {
            get; set;
        }
        /// <summary>Best-effort peak resident memory of that root process in MiB.</summary>
        public double? PeakMemoryMb
        {
            get; set;
        }
        /// <summary>Captured stdout/stderr lines, including runner-generated process diagnostics.</summary>
        public long LogLines
        {
            get; set;
        }
        /// <summary>Driver-reported coverage/core discriminator retained for diagnostics.</summary>
        public string Core
        {
            get; set;
        }
        /// <summary>Driver-reported coverage summary without relaxing the explicit terminal contract.</summary>
        public string Coverage
        {
            get; set;
        }
        /// <summary>Concise parent/driver explanation of failure or unusual admission.</summary>
        public string Note
        {
            get; set;
        }
        /// <summary>Stable classification for assertion, contract, context, input or process infrastructure failure.</summary>
        public string FailureKind
        {
            get; set;
        }
        /// <summary>Full process output of this test (<c>tests/artifacts/logs/&lt;name&gt;.log</c>), null when not saved.</summary>
        public string LogFile
        {
            get; set;
        }
        /// <summary>Error lines (or the output tail) shown under a failed test in the summary.</summary>
        public List<string> Diagnostics { get; set; } = new();
    }
}
