namespace Cthangover.TestRunner
{
    using System.Globalization;
    using System.Xml.Linq;

    /// <summary>Reads counters and individual outcomes, preserving NotExecuted skip reasons and failure stacks.</summary>
    public static class TrxParser
    {
        public static TrxReport Read(string path)
        {
            var report = new TrxReport();
            if (!File.Exists(path)) { report.Problems.Add("missing TRX report"); return report; }
            try
            {
                var document = XDocument.Load(path);
                var summary = document.Descendants().SingleOrDefault(element => element.Name.LocalName == "ResultSummary");
                var outcome = (string)summary?.Attribute("outcome");
                if (outcome is not ("Completed" or "Passed" or "Failed")) report.Problems.Add("TRX run did not complete: " + outcome);
                var counters = document.Descendants().SingleOrDefault(e => e.Name.LocalName == "Counters");
                if (counters == null) { report.Problems.Add("missing TRX counters"); return report; }
                foreach (var result in document.Descendants().Where(e => e.Name.LocalName == "UnitTestResult"))
                {
                    var caseOutcome = (string)result.Attribute("outcome");
                    var item = new TestCaseResult
                    {
                        Name = (string)result.Attribute("testName"),
                        Status = caseOutcome == "Passed" ? "pass" : caseOutcome is "NotExecuted" or "Skipped" ? "skipped" : "fail",
                        DurationSeconds = TimeSpan.TryParse((string)result.Attribute("duration"), CultureInfo.InvariantCulture, out var elapsed) ? elapsed.TotalSeconds : 0,
                        Message = result.Descendants().FirstOrDefault(e => e.Name.LocalName == "Message")?.Value,
                        StackTrace = result.Descendants().FirstOrDefault(e => e.Name.LocalName == "StackTrace")?.Value,
                    };
                    var output = result.Descendants().FirstOrDefault(e => e.Name.LocalName == "StdOut")?.Value;
                    if (output != null) item.Log.Add(output);
                    if (item.Status == "skipped" && item.Message == null) item.Message = output ?? caseOutcome;
                    report.Cases.Add(item);
                }
                report.Passed = report.Cases.Count(c => c.Status == "pass");
                report.Failed = report.Cases.Count(c => c.Status == "fail");
                report.Skipped = report.Cases.Count(c => c.Status == "skipped");
                if ((outcome == "Failed") != (report.Failed > 0)) report.Problems.Add("TRX run outcome disagrees with its case results");
                if (report.Cases.Count == 0) report.Problems.Add("empty TRX: no tests discovered or matched");
                if (ReadCounter(counters, "total", report) != report.Cases.Count || ReadCounter(counters, "passed", report) != report.Passed)
                    report.Problems.Add("TRX counters do not match individual results");
                var failed = ReadCounter(counters, "failed", report);
                var skipped = counters.Attribute("notExecuted") != null ? ReadCounter(counters, "notExecuted", report)
                    : counters.Attribute("skipped") != null ? ReadCounter(counters, "skipped", report) : 0;
                if (failed != report.Failed || skipped != report.Skipped) report.Problems.Add("TRX failure/skip counters do not match individual results");
            }
            catch (Exception error) { report.Problems.Add("invalid TRX: " + error.Message); }
            return report;
        }

        private static int ReadCounter(XElement counters, string name, TrxReport report)
        {
            if (int.TryParse((string)counters.Attribute(name), NumberStyles.None, CultureInfo.InvariantCulture, out var value)) return value;
            report.Problems.Add("invalid TRX counter: " + name);
            return -1;
        }
    }
}
