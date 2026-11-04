namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using Cthangover.TestRunner;

    /// <summary>TRX is mandatory execution evidence; NotExecuted is preserved instead of disappearing from totals.</summary>
    public class TrxContractTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), "trx_contract_" + Guid.NewGuid().ToString("N") + ".trx");

        [Fact]
        public void ReadsFailuresAndNotExecutedWithTheirMessages()
        {
            File.WriteAllText(path, """
                <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
                  <Results>
                    <UnitTestResult testName="pass" outcome="Passed" duration="00:00:00.125" />
                    <UnitTestResult testName="fail" outcome="Failed"><Output><ErrorInfo><Message>expected 1</Message><StackTrace>file:3</StackTrace></ErrorInfo></Output></UnitTestResult>
                    <UnitTestResult testName="skip" outcome="NotExecuted"><Output><ErrorInfo><Message>requires display</Message></ErrorInfo></Output></UnitTestResult>
                  </Results>
                  <ResultSummary outcome="Failed"><Counters total="3" passed="1" failed="1" notExecuted="1" /></ResultSummary>
                </TestRun>
                """);
            var report = TrxParser.Read(path);
            Assert.Empty(report.Problems);
            Assert.Equal(1, report.Passed); Assert.Equal(1, report.Failed); Assert.Equal(1, report.Skipped);
            Assert.Equal("requires display", report.Cases[2].Message);
            Assert.Equal("file:3", report.Cases[1].StackTrace);
            Assert.Equal(0.125, report.Cases[0].DurationSeconds);
        }

        [Theory]
        [InlineData("<TestRun />")]
        [InlineData("<TestRun><Counters total=\"0\" passed=\"0\" failed=\"0\" /></TestRun>")]
        [InlineData("<TestRun><UnitTestResult testName=\"p\" outcome=\"Passed\"/><Counters total=\"2\" passed=\"2\" failed=\"0\" /></TestRun>")]
        [InlineData("not xml")]
        [InlineData("<TestRun><UnitTestResult testName=\"p\" outcome=\"Passed\"/><ResultSummary outcome=\"Aborted\"><Counters total=\"1\" passed=\"1\" failed=\"0\" /></ResultSummary></TestRun>")]
        public void MissingEmptyOrContradictoryEvidenceIsAnError(string xml)
        {
            File.WriteAllText(path, xml);
            Assert.NotEmpty(TrxParser.Read(path).Problems);
        }

        [Fact] public void MissingFileIsAnError() => Assert.NotEmpty(TrxParser.Read(path).Problems);
        public void Dispose() { if (File.Exists(path)) File.Delete(path); }
    }
}
