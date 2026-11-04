namespace Cthangover.Tests.Testing
{
    using Cthangover.TestRunner;
    using System.Text.Json;

    /// <summary>Completion evidence must agree across the marker, counters and process, including negative paths.</summary>
    public class ResultContractTests
    {
        [Theory]
        [InlineData("", false)]
        [InlineData("[TEST_RESULT] name=other passed=1 failed=0", false)]
        [InlineData("[TEST_RESULT] name=probe passed=1 failed=0", true)]
        [InlineData("[TEST_RESULT] name=probe passed=0 failed=0", false)]
        [InlineData("[TEST_RESULT] name=probe passed=-1 failed=0", false)]
        [InlineData("[TEST_RESULT] name=probe passed=999999999999999 failed=0", false)]
        [InlineData("[TEST_RESULT] name=probe passed=1", false)]
        [InlineData("[TEST_RESULT] name=probe passed=1 failed=0 failed=1", false)]
        [InlineData("[TEST_RESULT] name=probe passed=1 failed=0 typo=1", false)]
        public void TerminalEvidence(string line, bool expected)
        {
            var result = ResultParser.Evaluate("entry", "integration", new ProcessResult { Output = new() { line } }, new ResultContract { Name = "probe" });
            Assert.Equal(expected, result.Passed);
            if (!expected) Assert.NotEmpty(result.Note);
        }

        [Theory]
        [InlineData(1, false)]
        [InlineData(0, true)]
        public void AGoodMarkerCannotOverrideProcessFailure(int exitCode, bool timeout)
        {
            var run = new ProcessResult { ExitCode = exitCode, TimedOut = timeout, Output = new() { "[TEST_RESULT] name=probe passed=3 failed=0" } };
            Assert.False(ResultParser.Evaluate("probe", "integration", run, new ResultContract { Name = "probe" }).Passed);
        }

        [Fact]
        public void LaterSuccessCannotEraseFailure()
        {
            var run = new ProcessResult { Output = new() { "[TEST_RESULT] name=probe passed=0 failed=1", "[TEST_RESULT] name=probe passed=1 failed=0" } };
            var result = ResultParser.Evaluate("probe", "integration", run, new ResultContract { Name = "probe" });
            Assert.False(result.Passed);
            Assert.Contains("duplicate", result.Note);
            Assert.Contains("failure", result.Note);
        }

        [Fact]
        public void ExplicitAliasAndCoreMustMatch()
        {
            var run = new ProcessResult { Output = new() { "[TEST_RESULT] name=battle core=ff_battle passed=1 failed=0" } };
            Assert.True(ResultParser.Evaluate("battle_ff", "integration", run, new ResultContract { Name = "battle", Core = "ff_battle" }).Passed);
            Assert.False(ResultParser.Evaluate("battle_ff", "integration", run, new ResultContract { Name = "battle", Core = "card_battle" }).Passed);
        }

        [Fact]
        public void SkippedIsVisibleAndDistinctFromPassingChecks()
        {
            var run = new ProcessResult { Output = new() { "[TEST_RESULT] name=probe passed=0 failed=0 skipped=2" } };
            var result = ResultParser.Evaluate("probe", "integration", run, new ResultContract { Name = "probe" });
            Assert.True(result.Passed);
            Assert.Equal("skipped", result.Status);
            Assert.Equal(2, result.SkippedChecks);
            Assert.False(ResultParser.Evaluate("probe", "integration", run, new ResultContract { Name = "probe", AllowSkipped = false }).Passed);
        }

        [Theory]
        [InlineData(2, 0, true)]
        [InlineData(1, 0, false)]
        [InlineData(2, 1, false)]
        public void AggregateMustAgreeWithChildren(int terminalPassed, int childFailed, bool expected)
        {
            var run = new ProcessResult { Output = new()
            {
                $"[TEST_RESULT] name=mod_units:one passed=2 failed={childFailed}",
                $"[TEST_RESULT] name=mod_units passed={terminalPassed} failed=0",
            } };
            Assert.Equal(expected, ResultParser.Evaluate("mod_units", "mod-unit", run, new ResultContract { Name = "mod_units", ChildPrefix = "mod_units:" }).Passed);
        }

        [Fact]
        public void EngineErrorAfterAPassingMarkerStillFailsTheRun()
        {
            var run = new ProcessResult { Output = new() { "[TEST_RESULT] name=probe passed=1 failed=0", "ERROR: resource leaked at exit" } };
            var result = ResultParser.Evaluate("probe", "integration", run, new ResultContract { Name = "probe" });
            Assert.False(result.Passed);
            Assert.Contains("unexpected engine error", result.Note);
        }

        [Theory]
        [InlineData("WARNING: ObjectDB instances leaked at exit.", true)]
        [InlineData("WARNING: 2 resources still in use at exit.", true)]
        [InlineData("WARNING: optional renderer feature unavailable", false)]
        public void ShutdownResourceWarningsCannotBeHiddenBySuccess(string diagnostic, bool failure)
        {
            var run = new ProcessResult { Output = new() { "[TEST_RESULT] name=probe passed=1 failed=0", diagnostic } };
            Assert.Equal(failure, !ResultParser.Evaluate("probe", "integration", run, new ResultContract { Name = "probe" }).Passed);
        }

        [Fact]
        public void DeliberateEngineErrorsHaveSpecificCountedExpectations()
        {
            var contract = new ResultContract { Name = "probe", ExpectedErrors = new() { new DiagnosticExpectation { Pattern = "^ERROR: deliberate invalid input$", Count = 1 } } };
            var run = new ProcessResult { Output = new() { "ERROR: deliberate invalid input", "[TEST_RESULT] name=probe passed=1 failed=0" } };
            Assert.True(ResultParser.Evaluate("probe", "integration", run, contract).Passed);
            run.Output.Add("ERROR: unrelated failure");
            Assert.False(ResultParser.Evaluate("probe", "integration", run, contract).Passed);
            run.Output.RemoveAt(2);
            run.Output.Add("ERROR: deliberate invalid input");
            Assert.False(ResultParser.Evaluate("probe", "integration", run, contract).Passed);
        }

        [Fact]
        public void DiagnosticContractSurvivesJsonRoundTrip()
        {
            var contract = new ResultContract { Name = "probe", Core = "core", ChildPrefix = "probe:", AllowSkipped = false,
                ExpectedErrors = new() { new DiagnosticExpectation { Pattern = "^ERROR: deliberate$", Count = 2 } } };
            var copy = JsonSerializer.Deserialize<ResultContract>(JsonSerializer.Serialize(contract, ReportWriter.JsonOptions), ReportWriter.JsonOptions);
            Assert.Equal(contract.Name, copy.Name); Assert.Equal(contract.Core, copy.Core); Assert.Equal(contract.ChildPrefix, copy.ChildPrefix);
            Assert.False(copy.AllowSkipped);
            Assert.Equal("^ERROR: deliberate$", Assert.Single(copy.ExpectedErrors).Pattern);
            Assert.Equal(2, copy.ExpectedErrors[0].Count);
        }
    }
}
