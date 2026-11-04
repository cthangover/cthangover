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
        public string Name { get; set; }
        public string Kind { get; set; }
        [JsonPropertyName("success")] public bool Passed { get; set; }
        public bool TimedOut { get; set; }
        public int ExitCode { get; set; }
        [JsonPropertyName("passed")] public int PassedChecks { get; set; }
        [JsonPropertyName("failed")] public int FailedChecks { get; set; }
        [JsonPropertyName("skipped")] public int SkippedChecks { get; set; }
        public bool Blocked { get; set; }
        public string Filter { get; set; }
        public string ModFilter { get; set; }
        public string Environment { get; set; }
        public string SelectionReason { get; set; }
        public string[] DependsOn { get; set; } = Array.Empty<string>();
        public List<TestCaseResult> Cases { get; set; } = new();
        public List<string> Artifacts { get; set; } = new();
        public string Status => Blocked ? "blocked" : TimedOut ? "timeout" : !Passed ? "fail"
            : PassedChecks == 0 && SkippedChecks > 0 ? "skipped" : "pass";
        [JsonIgnore] public TimeSpan Duration { get; set; }
        public double DurationSeconds { get => Duration.TotalSeconds; set => Duration = TimeSpan.FromSeconds(value); }
        public string Core { get; set; }
        public string Coverage { get; set; }
        public string Note { get; set; }
        public string FailureKind { get; set; }
        /// <summary>Full process output of this test (<c>tests/artifacts/logs/&lt;name&gt;.log</c>), null when not saved.</summary>
        public string LogFile { get; set; }
        /// <summary>Error lines (or the output tail) shown under a failed test in the summary.</summary>
        public List<string> Diagnostics { get; set; } = new();
    }
}
